using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ShinoCodexMonitor
{
    // Rollout JSONL is a local, version-sensitive observation source, not an official live-status API.
    // Never retain user prompts, message bodies, tool arguments, or raw rollout lines.
    internal enum LiveTaskPhase { Unknown, Active, Completed, Failed, Interrupted, Unconfirmed }

    internal sealed class LiveTask
    {
        internal string Id = "";
        internal bool IdentityUncertain;
        internal string Identity { get { return SessionKey + "|" + StartedUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) + "|" + Id; } }
        internal LiveTask Copy() { return (LiveTask)MemberwiseClone(); }
        internal string SessionKey = ""; // rollout filename only; never path, content or credentials
        internal string ThreadSource = "Unknown";
        internal string Originator = "Not provided";
        internal bool IsSubagent;
        internal bool IsRootConfirmed;
        internal long? RecordedDurationMs;
        internal string Workspace = "Project not provided";
        internal string Model = "Not provided";
        internal string Effort = "Not provided";
        internal LiveTaskPhase Phase;
        internal DateTimeOffset StartedUtc;
        internal DateTimeOffset? FinishedUtc;
        internal DateTimeOffset LastWriteUtc;
        internal string Source = "Local Codex / JSONL sessions (undocumented)";
        internal string LastAction = "Activity details unavailable";

        internal string PhaseLabel
        {
            get
            {
                switch (Phase)
                {
                    case LiveTaskPhase.Active: return "RUNNING · observed";
                    case LiveTaskPhase.Completed: return "COMPLETED";
                    case LiveTaskPhase.Failed: return "FAILED";
                    case LiveTaskPhase.Interrupted: return "INTERRUPTED";
                    case LiveTaskPhase.Unconfirmed: return "UNVERIFIED";
                    default: return "UNAVAILABLE";
                }
            }
        }

        internal string Duration(DateTimeOffset now)
        {
            DateTimeOffset end = FinishedUtc ?? (Phase == LiveTaskPhase.Active ? now : LastWriteUtc);
            if (end < StartedUtc) return "—";
            TimeSpan span = FinishedUtc.HasValue && RecordedDurationMs.HasValue &&
                RecordedDurationMs.Value >= 0 && RecordedDurationMs.Value <= 86400000L ?
                TimeSpan.FromMilliseconds(RecordedDurationMs.Value) : end - StartedUtc;
            string prefix = Phase == LiveTaskPhase.Unconfirmed ? "≥ " : "";
            if (span.TotalDays >= 1) return prefix + ((int)span.TotalDays) + " d " + span.Hours + " h";
            if (span.TotalHours >= 1) return prefix + ((int)span.TotalHours) + " h " + span.Minutes.ToString("00") + " min";
            return prefix + ((int)span.TotalMinutes).ToString("00") + ":" + span.Seconds.ToString("00");
        }
    }

    internal enum TaskDisplayKind { ObservedActive, Uncertain, RecentTerminal, NotVerified }

    // Presentation is a SEPARATE projection. A completed local turn does not
    // indicate that the currently selected Codex Desktop thread is finished.
    internal sealed class TaskDisplay
    {
        internal TaskDisplayKind Kind = TaskDisplayKind.NotVerified;
        internal LiveTask Primary;
        internal LiveTask LastTerminal;
        internal string Detail = "Current activity not verifiable";
    }

    internal sealed class LiveTaskReport
    {
        internal readonly List<LiveTask> Tasks = new List<LiveTask>();
        internal string Message = "No recent local Codex session";
        internal DateTimeOffset CheckedUtc;
        internal int ExaminedRoots;
        internal int IgnoredSubagents;
        internal int UnknownOrigins;
        internal int ActiveCount { get { return Tasks.Count(t => t.Phase == LiveTaskPhase.Active); } }
        internal LiveTask Featured
        {
            get
            {
                // Legacy raw reporting only; NEVER use as current-status evidence.
                return Tasks.OrderBy(t => t.Phase == LiveTaskPhase.Active ? 0 :
                    t.Phase == LiveTaskPhase.Unconfirmed ? 1 : 2)
                    .ThenByDescending(t => t.FinishedUtc ?? t.StartedUtc).FirstOrDefault();
            }
        }

        internal string RedactedDiagnostics(DateTimeOffset now)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("CODEX PERISCOPE V5.2 — READ-ONLY TASK DIAGNOSTICS");
            text.AppendLine("Reading (UTC): " +
                (CheckedUtc == DateTimeOffset.MinValue ? "none" :
                    CheckedUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
            text.AppendLine("Root sessions checked: " + ExaminedRoots +
                " ; child sessions excluded: " + IgnoredSubagents +
                " ; unknown origins: " + UnknownOrigins);
            TaskDisplay display = DisplayAt(now);
            text.AppendLine("Observed status: " + display.Kind + " ; " + display.Detail);
            int i = 0;
            foreach (LiveTask task in Tasks.OrderByDescending(t => t.StartedUtc).Take(6))
            {
                // No session UUID, full path, cwd, user message, command or token.
                text.AppendLine("Turn #" + (++i) + " : " +
                    task.Phase + " ; source=" + task.ThreadSource +
                    " ; started (UTC)=" + task.StartedUtc.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture) +
                    " ; completed (UTC)=" +
                    (task.FinishedUtc.HasValue ?
                        task.FinishedUtc.Value.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "absent") +
                    " ; duration=" + task.Duration(now) +
                    " ; duration_ms=" + (task.RecordedDurationMs.HasValue ?
                        task.RecordedDurationMs.Value.ToString(CultureInfo.InvariantCulture) : "missing") +
                    " ; freshness (s)=" + Math.Max(0, (int)(now - task.LastWriteUtc).TotalSeconds));
            }
            if (i == 0) text.AppendLine("No primary turn observed.");
            text.AppendLine("No prompts, paths, session IDs or secrets copied.");
            return text.ToString();
        }

        internal TaskDisplay DisplayAt(DateTimeOffset now)
        {
            TaskDisplay display = new TaskDisplay();
            // Do not silently keep presenting a cached green check / active wheel
            // when the observer has stopped producing valid snapshots.
            double scanAge = (now - CheckedUtc).TotalSeconds;
            if (CheckedUtc == DateTimeOffset.MinValue || scanAge < -5 || scanAge > 25)
            {
                display.Detail = "Task reading stale or unavailable";
                return display;
            }

            display.LastTerminal = Tasks.Where(t =>
                (t.Phase == LiveTaskPhase.Completed || t.Phase == LiveTaskPhase.Failed ||
                    t.Phase == LiveTaskPhase.Interrupted) &&
                t.FinishedUtc.HasValue && t.FinishedUtc.Value <= now.AddSeconds(5))
                .OrderByDescending(t => t.FinishedUtc.Value).FirstOrDefault();

            LiveTask active = Tasks.Where(t => t.Phase == LiveTaskPhase.Active &&
                t.StartedUtc <= now.AddSeconds(5) &&
                (now - t.LastWriteUtc).TotalSeconds >= -5 &&
                (now - t.LastWriteUtc).TotalSeconds <= LiveTaskReader.ActivityMaxAgeSeconds)
                .OrderByDescending(t => t.StartedUtc).FirstOrDefault();
            if (active != null)
            {
                display.Primary = active;
                display.Kind = TaskDisplayKind.ObservedActive;
                display.Detail = "Local start event · recent activity";
                return display;
            }

            LiveTask uncertain = Tasks.Where(t => t.Phase == LiveTaskPhase.Unconfirmed &&
                t.StartedUtc <= now.AddSeconds(5) &&
                (now - t.LastWriteUtc).TotalMinutes <= 30 &&
                (now - t.LastWriteUtc).TotalSeconds >= -5)
                .OrderByDescending(t => t.StartedUtc).FirstOrDefault();
            if (uncertain != null)
            {
                display.Primary = uncertain;
                display.Kind = TaskDisplayKind.Uncertain;
                display.Detail = "Start recorded, but activity unverified";
                return display;
            }

            // Briefly show the most recently CONFIRMED terminal event, but label it
            // as a *past local turn*, not the current Codex application state.
            if (display.LastTerminal != null)
            {
                double elapsed = (now - display.LastTerminal.FinishedUtc.Value).TotalSeconds;
                if (elapsed >= -5 && elapsed <= 30)
                {
                    display.Primary = display.LastTerminal;
                    display.Kind = TaskDisplayKind.RecentTerminal;
                    display.Detail = "Last local turn completed, not the global Codex status";
                    return display;
                }
                display.Detail = "Last recorded turn: completed; current activity unknown";
            }
            else
                display.Detail = "No running task confirmed in logs";
            return display;
        }
    }

    // Notifications are inferred from CONFIRMED terminal events in one local rollout.
    // The real Codex task_started event may have no turn_id; identify its session
    // by the rollover filename plus the persisted start timestamp. Never match by
    // workspace/model or filesystem modification time alone.
    internal static class TaskNotificationRules
    {
        private static bool IsTerminal(LiveTask task)
        {
            return task != null && (task.Phase == LiveTaskPhase.Completed ||
                task.Phase == LiveTaskPhase.Failed || task.Phase == LiveTaskPhase.Interrupted);
        }

        internal static bool SameTurn(LiveTask a, LiveTask b)
        {
            if (a == null || b == null || a.IdentityUncertain || b.IdentityUncertain || string.IsNullOrEmpty(a.SessionKey) ||
                string.IsNullOrEmpty(b.SessionKey) ||
                !string.Equals(a.SessionKey, b.SessionKey, StringComparison.Ordinal) ||
                a.StartedUtc != b.StartedUtc) return false;
            // Prefer turn_id when BOTH snapshots contain one; Codex sometimes omits it.
            return string.IsNullOrEmpty(a.Id) || string.IsNullOrEmpty(b.Id) ||
                string.Equals(a.Id, b.Id, StringComparison.Ordinal);
        }

        internal static List<LiveTask> NewTerminals(LiveTaskReport previous, LiveTaskReport current)
        {
            List<LiveTask> found = new List<LiveTask>();
            if (previous == null || current == null) return found;
            TimeSpan pollingGap = current.CheckedUtc - previous.CheckedUtc;
            // No replay after app resume, reader errors, startup or a long polling gap.
            if (pollingGap < TimeSpan.Zero || pollingGap > TimeSpan.FromSeconds(30)) return found;
            foreach (LiveTask task in current.Tasks)
            {
                if (task.IdentityUncertain || !IsTerminal(task) || !task.FinishedUtc.HasValue ||
                    task.FinishedUtc.Value < task.StartedUtc ||
                    string.IsNullOrEmpty(task.SessionKey) ||
                    // An old terminal event is history, not a new notification.
                    task.FinishedUtc.Value < previous.CheckedUtc.AddSeconds(-5) ||
                    task.FinishedUtc.Value > current.CheckedUtc.AddSeconds(5)) continue;
                bool wasActive = previous.Tasks.Any(old => old.Phase == LiveTaskPhase.Active &&
                    SameTurn(old, task));
                bool wasAlreadySeen = previous.Tasks.Any(old => SameTurn(old, task));
                // A short complete turn may start AND finish between 5 s polls.
                // This path requires a valid completed event with both timestamps,
                // observed AFTER the previous successful scan (not old history).
                bool briefTurn = !wasAlreadySeen &&
                    task.StartedUtc >= previous.CheckedUtc &&
                    task.FinishedUtc.Value >= previous.CheckedUtc &&
                    task.FinishedUtc.Value <= current.CheckedUtc.AddSeconds(5);
                if (wasActive || briefTurn) found.Add(task);
            }
            return found;
        }
    }

    internal static class LiveTaskReader
    {
        internal const int ActivityMaxAgeSeconds = 90;
        private const int TailBytes = 524288;
        private const int ActiveRootFallbackBytes = 16777216; // only freshest root; 16 MiB max
        private const int MaxFiles = 12;
        private const int DiscoveryFiles = 48;
        internal const int MaxTurnsPerSession = 64;
        private sealed class SessionCache
        {
            internal long Length;
            internal DateTime Write;
            internal List<LiveTask> Turns;
        }
        private static readonly Dictionary<string,SessionCache> Sessions = new Dictionary<string,SessionCache>(StringComparer.OrdinalIgnoreCase);
        private static readonly object ReadGate = new object();
        // Immutable, sanitized metadata only; never keep the original session_meta,
        // system prompt, instructions, auth or response content in this cache.
        private static readonly Dictionary<string, string> HeaderCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static string SessionRoot()
        {
            string home = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (string.IsNullOrWhiteSpace(home))
                home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            return Path.Combine(home, "sessions");
        }

        internal static LiveTaskReport Read(DateTimeOffset now)
        { lock (ReadGate) return ReadCore(now); }
        private static LiveTaskReport ReadCore(DateTimeOffset now)
        {
            LiveTaskReport report = new LiveTaskReport { CheckedUtc = now };
            string root = SessionRoot();
            if (!Directory.Exists(root))
            {
                report.Message = "Unavailable · none Codex local sessions directory";
                return report;
            }
            try
            {
                List<FileInfo> candidates = new List<FileInfo>();
                for (int day = -3; day <= 1; day++)
                {
                    DateTime date = now.UtcDateTime.Date.AddDays(day);
                    string folder = Path.Combine(root, date.ToString("yyyy", CultureInfo.InvariantCulture),
                        date.ToString("MM", CultureInfo.InvariantCulture),
                        date.ToString("dd", CultureInfo.InvariantCulture));
                    if (!Directory.Exists(folder)) continue;
                    foreach (string file in Directory.EnumerateFiles(folder, "rollout-*.jsonl", SearchOption.TopDirectoryOnly))
                    {
                        FileInfo info = new FileInfo(file);
                        if (info.LastWriteTimeUtc >= now.UtcDateTime.AddHours(-24) &&
                            info.LastWriteTimeUtc <= now.UtcDateTime.AddMinutes(2))
                            candidates.Add(info);
                    }
                }
                int checkedRoots = 0;
                int ignoredChildren = 0;
                int unknownHeaders = 0;
                int expansionBudget = ActiveRootFallbackBytes;
                foreach (FileInfo info in candidates.OrderByDescending(f => f.LastWriteTimeUtc)
                    .Take(DiscoveryFiles))
                {
                    try
                    {
                        // Root session may have less recent disk activity than 12 subagents.
                        // Inspect metadata FIRST, then parse a bounded tail only for roots.
                        SessionCache priorSource;
                        if (Sessions.TryGetValue(info.FullName,out priorSource) && info.Length < priorSource.Length) HeaderCache.Remove(info.FullName);
                        string sessionMeta = ReadFirstMetadataLine(info.FullName);
                        if (sessionMeta == null) { unknownHeaders++; continue; }
                        string threadKind = HeaderThreadKind(sessionMeta);
                        if (threadKind == "subagent") { ignoredChildren++; continue; }
                        if (threadKind != "user") { unknownHeaders++; continue; }
                        if (++checkedRoots > MaxFiles) break;
                        SessionCache cache;
                        bool known = Sessions.TryGetValue(info.FullName,out cache);
                        List<LiveTask> turns;
                        if (known && cache.Length == info.Length && cache.Write == info.LastWriteTimeUtc)
                            turns = cache.Turns.Select(t => t.Copy()).ToList();
                        else {
                            IEnumerable<LiveTask> seed = known && info.Length >= cache.Length ? cache.Turns : null;
                            turns = ParseCollection(new [] {sessionMeta}.Concat(ReadTail(info.FullName,TailBytes)),
                                new DateTimeOffset(info.LastWriteTimeUtc,TimeSpan.Zero),now,seed);
                            // The existing global fallback bound is retained. Warm sources keep
                            // sanitized starts instead of expanding repeatedly as tails grow.
                            if (turns.Count == 0 && expansionBudget > 0 && info.LastWriteTimeUtc >= now.UtcDateTime.AddMinutes(-2)) {
                                int allowance=Math.Min(4*1024*1024,expansionBudget);
                                expansionBudget-=allowance;
                                turns = ParseTurns(new [] {sessionMeta}.Concat(ReadTail(info.FullName,allowance)),
                                    new DateTimeOffset(info.LastWriteTimeUtc,TimeSpan.Zero),now);
                            }
                            if (Sessions.Count >= 64 && !known) Sessions.Clear();
                            Sessions[info.FullName] = new SessionCache { Length = info.Length, Write = info.LastWriteTimeUtc,
                                Turns = turns.Select(t => t.Copy()).ToList() };
                        }
                        foreach (LiveTask task in turns) {
                            task.SessionKey = info.Name;
                            Expire(task,now);
                            if (!task.IsRootConfirmed || task.IsSubagent) continue;
                            if (task.Phase == LiveTaskPhase.Active ||
                                (task.Phase == LiveTaskPhase.Unconfirmed && now-task.LastWriteUtc < TimeSpan.FromMinutes(30)) ||
                                now-(task.FinishedUtc ?? task.StartedUtc) < TimeSpan.FromHours(2)) report.Tasks.Add(task);
                        }
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (ArgumentException) { }
                }
                report.Tasks.Sort(delegate(LiveTask x, LiveTask y) {
                    return y.LastWriteUtc.CompareTo(x.LastWriteUtc);
                });
                report.ExaminedRoots = checkedRoots;
                report.IgnoredSubagents = ignoredChildren;
                report.UnknownOrigins = unknownHeaders;
                report.Message = "Local root turns · " + checkedRoots +
                    " checked, " + ignoredChildren + " subagents excluded" +
                    (unknownHeaders > 0 ? " · " + unknownHeaders + " unclassified sources" : "");
                if (report.Tasks.Count == 0)
                    report.Message += " · current turn unverified";
            }
            catch (UnauthorizedAccessException) { report.Message = "Unavailable · folder access denied"; }
            catch (IOException) { report.Message = "Unverified · session read interrupted"; }
            return report;
        }

        private static string HeaderThreadKind(string sanitizedMeta)
        {
            try
            {
                Dictionary<string, object> root = Json.Object(new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 1048576 }.DeserializeObject(sanitizedMeta));
                Dictionary<string, object> payload = Json.Object(Json.Get(root, "payload"));
                if (payload == null) return "unknown";
                string kind = Json.String(Json.Get(payload, "thread_source"));
                if (kind == "subagent") return "subagent";
                if (!string.IsNullOrEmpty(Json.String(Json.Get(payload, "parent_thread_id"))))
                    return "subagent";
                return string.Equals(kind, "user", StringComparison.OrdinalIgnoreCase) ? "user" : "unknown";
            }
            catch (ArgumentException) { return "unknown"; }
        }

        private static string ReadFirstMetadataLine(string path)
        {
            string cached;
            if (HeaderCache.TryGetValue(path, out cached)) return cached;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false), true, 4096))
                {
                    // Bounded first-record read, once per source file, regardless of
                    // the amount of reasoning/tool output appended over 10+ minutes.
                    StringBuilder line = new StringBuilder();
                    int ch;
                    while (line.Length < 1048576 && (ch = reader.Read()) >= 0)
                    {
                        if (ch == '\n') break;
                        if (ch != '\r') line.Append((char)ch);
                    }
                    if (line.Length == 0 || line.Length >= 1048576) return null;
                    Dictionary<string, object> root = Json.Object(new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 1048576 }.DeserializeObject(line.ToString()));
                    if (root == null || Json.String(Json.Get(root, "type")) != "session_meta") return null;
                    Dictionary<string, object> payload = Json.Object(Json.Get(root, "payload"));
                    if (payload == null) return null;

                    // Preserve ONLY narrow metadata. The raw session header may
                    // contain confidential system instructions; discard immediately.
                    Dictionary<string, object> clean = new Dictionary<string, object>();
                    foreach (string field in new [] { "thread_source", "originator", "parent_thread_id", "cwd" })
                    {
                        object value = Json.Get(payload, field);
                        if (value is string)
                        {
                            string raw = (string)value;
                            if (field == "cwd")
                            {
                                string normalized = raw.Replace('\\', '/').TrimEnd('/');
                                raw = normalized.Substring(normalized.LastIndexOf('/') + 1);
                            }
                            if (raw.Length <= 1024) clean[field] = raw;
                        }
                    }
                    // Non-string thread_source is deliberately not trusted as root.
                    string safe = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new Dictionary<string, object> {
                        { "type", "session_meta" }, { "payload", clean }
                    });
                    if (HeaderCache.Count >= 256) HeaderCache.Clear();
                    HeaderCache[path] = safe;
                    return safe;
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
        }

        private static IEnumerable<string> ReadTail(string path, int maxBytes)
        {
            List<string> records = new List<string>();
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                long start = Math.Max(0, stream.Length - maxBytes);
                stream.Seek(start, SeekOrigin.Begin);
                using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false), true, 4096))
                {
                    if (start > 0) reader.ReadLine(); // discard incomplete first record
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length <= 1048576 && Relevant(line))
                            records.Add(line);
                    }
                }
            }
            return records;
        }

        // Legacy one-turn API remains for compatibility tests; production consumes ParseTurns.
        internal static LiveTask ParseLines(IEnumerable<string> lines, DateTimeOffset fileWriteUtc, DateTimeOffset now)
        { return ParseTurns(lines,fileWriteUtc,now).LastOrDefault(); }
        internal static List<LiveTask> ParseTurns(IEnumerable<string> lines, DateTimeOffset fileWriteUtc, DateTimeOffset now)
        { return ParseCollection(lines,fileWriteUtc,now,null); }
        private static List<LiveTask> ParseCollection(IEnumerable<string> lines, DateTimeOffset fileWriteUtc, DateTimeOffset now, IEnumerable<LiveTask> seed)
        {
            List<LiveTask> turns = seed == null ? new List<LiveTask>() : seed.Where(t =>
                t.FinishedUtc.HasValue ? now-t.FinishedUtc.Value < TimeSpan.FromHours(2) : now-t.LastWriteUtc < TimeSpan.FromMinutes(30)).Select(t => t.Copy()).ToList();
            System.Web.Script.Serialization.JavaScriptSerializer serializer = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 1048576 };
            string workspace = "Project not provided", originator = "Not provided", threadSource = "Unknown";
            bool isSubagent = false, isRoot = false;
            foreach (string line in lines) {
                if (string.IsNullOrEmpty(line) || line.Length > 1048576 || !Relevant(line)) continue;
                Dictionary<string,object> root;
                try { root = Json.Object(serializer.DeserializeObject(line)); } catch (ArgumentException) { continue; }
                if (root == null) continue;
                string type = Json.String(Json.Get(root,"type"));
                Dictionary<string,object> payload = Json.Object(Json.Get(root,"payload")); if (payload == null) continue;
                if (type == "session_meta") {
                    string cwd = Json.String(Json.Get(payload,"cwd"));
                    if (!string.IsNullOrEmpty(cwd)) { string path = cwd.Replace('\\','/').TrimEnd('/'); workspace = Clean(path.Substring(path.LastIndexOf('/')+1),workspace); }
                    originator = Clean(Json.String(Json.Get(payload,"originator")),originator);
                    object raw = Json.Get(payload,"thread_source");
                    string kind = raw as string; Dictionary<string,object> kindObject = Json.Object(raw);
                    if (kindObject != null && kindObject.Count > 0) kind = kindObject.Keys.FirstOrDefault();
                    threadSource = Clean(kind,threadSource);
                    isSubagent = !string.IsNullOrWhiteSpace(Json.String(Json.Get(payload,"parent_thread_id"))) ||
                        threadSource.IndexOf("subagent",StringComparison.OrdinalIgnoreCase) >= 0 || threadSource.IndexOf("sub_agent",StringComparison.OrdinalIgnoreCase) >= 0;
                    isRoot = !isSubagent && string.Equals(threadSource,"user",StringComparison.OrdinalIgnoreCase);
                    foreach (LiveTask t in turns) { t.Workspace=workspace; t.Originator=originator; t.ThreadSource=threadSource; t.IsSubagent=isSubagent; t.IsRootConfirmed=isRoot; }
                    continue;
                }
                string ev = type == "event_msg" ? Json.String(Json.Get(payload,"type")) : "";
                string rawId = Json.String(Json.Get(payload,"turn_id"));
                string id = ReliableId(rawId);
                DateTimeOffset time = Timestamp(root,now);
                if (ev == "task_started" || ev == "turn_started") {
                    if (time == DateTimeOffset.MinValue || time > now.AddSeconds(10)) continue;
                    long? epoch = Json.Long(Json.Get(payload,"started_at"));
                    if (epoch.HasValue) try {
                        DateTimeOffset candidate = DateTimeOffset.FromUnixTimeSeconds(epoch.Value);
                        if (candidate <= now.AddSeconds(10) && candidate >= time.AddMinutes(-5)) time = candidate;
                    } catch (ArgumentOutOfRangeException) { }
                    LiveTask existing = turns.FirstOrDefault(t => id.Length > 0 ? t.Id == id : t.StartedUtc == time);
                    if (existing != null) {
                        if (existing.StartedUtc != time) Uncertain(existing);
                        continue; // Repeated records in overlapping tails never create/restart a task.
                    }
                    List<LiveTask> open = turns.Where(t => !t.FinishedUtc.HasValue).ToList();
                    if ((id.Length == 0 && open.Count > 0) || open.Any(t => t.Id.Length == 0)) {
                        foreach (LiveTask t in open) Uncertain(t);
                        continue; // No reliable evidence for an additional distinct mission.
                    }
                    if (turns.Count >= MaxTurnsPerSession) {
                        LiveTask oldestTerminal=turns.Where(t => t.FinishedUtc.HasValue).OrderBy(t => t.FinishedUtc).FirstOrDefault();
                        if (oldestTerminal != null) turns.Remove(oldestTerminal);
                        else { foreach (LiveTask t in open) Uncertain(t); continue; }
                    }
                    turns.Add(new LiveTask { Id=id, Workspace=workspace, Originator=originator, ThreadSource=threadSource,
                        IsSubagent=isSubagent, IsRootConfirmed=isRoot, StartedUtc=time, LastWriteUtc=time,
                        Phase=LiveTaskPhase.Active, IdentityUncertain=!string.IsNullOrEmpty(rawId) && id.Length == 0 });
                    continue;
                }
                if (!string.IsNullOrEmpty(rawId) && id.Length == 0) { foreach (LiveTask t in turns.Where(t => !t.FinishedUtc.HasValue)) Uncertain(t); continue; }
                List<LiveTask> matches = id.Length > 0 ? turns.Where(t => t.Id == id).ToList() : turns.Where(t => !t.FinishedUtc.HasValue).ToList();
                if (matches.Count == 0 && type == "turn_context" && id.Length > 0) {
                    List<LiveTask> open=turns.Where(t => !t.FinishedUtc.HasValue).ToList();
                    if (open.Count == 1 && open[0].Id.Length == 0 && !open[0].IdentityUncertain && time >= open[0].StartedUtc) {
                        open[0].Id=id; matches.Add(open[0]);
                    }
                }
                // Duplicate anonymous terminal may refer to the only recorded turn.
                if (matches.Count == 0 && id.Length == 0 && turns.Count == 1) matches.Add(turns[0]);
                if (matches.Count != 1) {
                    if (id.Length == 0) foreach (LiveTask t in matches) Uncertain(t);
                    continue;
                }
                LiveTask task = matches[0];
                if (task.IdentityUncertain || time == DateTimeOffset.MinValue) continue;
                if (type == "turn_context" && !task.FinishedUtc.HasValue) {
                    if (time < task.StartedUtc) continue;
                    task.Model = Clean(Json.String(Json.Get(payload,"model")),task.Model);
                    string effort = Json.String(Json.Get(payload,"effort")) ?? Json.String(Json.Get(payload,"reasoning_effort")) ?? Json.String(Json.Get(payload,"model_reasoning_effort"));
                    Dictionary<string,object> mode = Json.Object(Json.Get(payload,"collaboration_mode"));
                    if (effort == null) effort = Json.String(Json.Get(Json.Object(Json.Get(mode,"settings")),"model_reasoning_effort"));
                    task.Effort=Clean(effort,task.Effort); task.LastWriteUtc = time > task.LastWriteUtc ? time : task.LastWriteUtc; task.Phase=LiveTaskPhase.Active;
                }
                else if (ev == "task_complete" || ev == "turn_complete" || ev == "turn_completed" || ev == "turn_aborted" || ev == "task_aborted") {
                    LiveTaskPhase phase = ev == "turn_aborted" || ev == "task_aborted" ? LiveTaskPhase.Interrupted : Json.Get(payload,"error") != null ? LiveTaskPhase.Failed : LiveTaskPhase.Completed;
                    if (time < task.StartedUtc || (task.FinishedUtc.HasValue && (task.FinishedUtc != time || task.Phase != phase))) { Uncertain(task); continue; }
                    task.Phase=phase; task.FinishedUtc=time; task.LastWriteUtc=time;
                    long? duration=Json.Long(Json.Get(payload,"duration_ms"));
                    if (duration.HasValue && duration.Value >= 0 && duration.Value <= 86400000L && Math.Abs((time-task.StartedUtc).TotalMilliseconds-duration.Value) <= 120000) task.RecordedDurationMs=duration;
                }
            }
            foreach (LiveTask task in turns) {
                // Generic file writes may support freshness only for a uniquely open turn
                // that never overlaps another known turn in this session.
                bool overlap = turns.Any(t => t != task && t.StartedUtc <= now && (t.FinishedUtc ?? now) >= task.StartedUtc);
                if (!task.FinishedUtc.HasValue && !task.IdentityUncertain && !overlap && fileWriteUtc >= task.LastWriteUtc) {
                    task.LastWriteUtc=fileWriteUtc; task.Phase=LiveTaskPhase.Active;
                }
                Expire(task,now);
            }
            return turns;
        }
        private static string ReliableId(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Length > 128 || value.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_')) return "";
            return value;
        }
        private static void Uncertain(LiveTask task)
        { task.IdentityUncertain=true; task.Phase=LiveTaskPhase.Unconfirmed; task.FinishedUtc=null; task.RecordedDurationMs=null; }
        private static void Expire(LiveTask task, DateTimeOffset now)
        {
            double age=(now-task.LastWriteUtc).TotalSeconds;
            if (task.IdentityUncertain || (task.Phase == LiveTaskPhase.Active && (age < -5 || age > ActivityMaxAgeSeconds))) task.Phase=LiveTaskPhase.Unconfirmed;
        }

        private static bool Relevant(string line)
        {
            // Exclude user messages, reasoning, tool arguments, output, token records.
            if (line.Contains("\"turn_context\"") || line.Contains("\"session_meta\""))
                return true;
            if (!line.Contains("\"event_msg\"")) return false;
            return line.Contains("\"task_started\"") || line.Contains("\"turn_started\"") ||
                line.Contains("\"task_complete\"") || line.Contains("\"turn_complete\"") ||
                line.Contains("\"turn_completed\"") || line.Contains("\"turn_aborted\"") ||
                line.Contains("\"task_aborted\"");
        }

        private static DateTimeOffset Timestamp(Dictionary<string, object> root, DateTimeOffset now)
        {
            DateTimeOffset timestamp;
            if (DateTimeOffset.TryParse(Json.String(Json.Get(root, "timestamp")), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out timestamp) &&
                timestamp > now.AddDays(-7) && timestamp <= now.AddSeconds(10))
                return timestamp;
            return DateTimeOffset.MinValue;
        }

        private static string Clean(string input, string fallback)
        {
            if (string.IsNullOrWhiteSpace(input)) return fallback;
            StringBuilder safe = new StringBuilder();
            foreach (char c in input)
                if (!char.IsControl(c)) safe.Append(c);
            string value = safe.ToString().Trim();
            return value.Length == 0 ? fallback : value.Substring(0, Math.Min(64, value.Length));
        }
    }
}
