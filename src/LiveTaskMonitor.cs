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
        internal string SessionKey = ""; // rollout filename only; never path, content or credentials
        internal string ThreadSource = "Inconnue";
        internal string Originator = "Not provided";
        internal bool IsSubagent;
        internal bool IsRootConfirmed;
        internal long? RecordedDurationMs;
        internal string Workspace = "Project unknown";
        internal string Model = "Not provided";
        internal string Effort = "Not provided";
        internal LiveTaskPhase Phase;
        internal DateTimeOffset StartedUtc;
        internal DateTimeOffset? FinishedUtc;
        internal DateTimeOffset LastWriteUtc;
        internal string Source = "Codex local / sessions JSONL (non documenté)";
        internal string LastAction = "Activity details unavailable";

        internal string PhaseLabel
        {
            get
            {
                switch (Phase)
                {
                    case LiveTaskPhase.Active: return "EN COURS · observé";
                    case LiveTaskPhase.Completed: return "TERMINÉ";
                    case LiveTaskPhase.Failed: return "ÉCHEC";
                    case LiveTaskPhase.Interrupted: return "INTERROMPU";
                    case LiveTaskPhase.Unconfirmed: return "UNVERIFIED";
                    default: return "INDISPONIBLE";
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
            if (span.TotalDays >= 1) return prefix + ((int)span.TotalDays) + " j " + span.Hours + " h";
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
        internal string Detail = "None activité actuelle vérifiable";
    }

    internal sealed class LiveTaskReport
    {
        internal readonly List<LiveTask> Tasks = new List<LiveTask>();
        internal string Message = "None session Codex locale récente";
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

        internal string RedactedDiagnosticss(DateTimeOffset now)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("CODEX PERISCOPE V5.1 — DIAGNOSTIC DES TOURS (LECTURE SEULE)");
            text.AppendLine("Relevé (UTC) : " +
                (CheckedUtc == DateTimeOffset.MinValue ? "aucun" :
                    CheckedUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
            text.AppendLine("Racines inspectées : " + ExaminedRoots +
                " ; enfants ignorés : " + IgnoredSubagents +
                " ; origines non classées : " + UnknownOrigins);
            TaskDisplay display = DisplayAt(now);
            text.AppendLine("État observé : " + display.Kind + " ; " + display.Detail);
            int i = 0;
            foreach (LiveTask task in Tasks.OrderByDescending(t => t.StartedUtc).Take(6))
            {
                // No session UUID, full path, cwd, user message, command or token.
                text.AppendLine("Tour #" + (++i) + " : " +
                    task.Phase + " ; source=" + task.ThreadSource +
                    " ; démarré (UTC)=" + task.StartedUtc.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture) +
                    " ; finished (UTC)=" +
                    (task.FinishedUtc.HasValue ?
                        task.FinishedUtc.Value.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "absent") +
                    " ; durée=" + task.Duration(now) +
                    " ; durée_ms=" + (task.RecordedDurationMs.HasValue ?
                        task.RecordedDurationMs.Value.ToString(CultureInfo.InvariantCulture) : "absente") +
                    " ; fraîcheur (s)=" + Math.Max(0, (int)(now - task.LastWriteUtc).TotalSeconds));
            }
            if (i == 0) text.AppendLine("Aucun tour principal observable.");
            text.AppendLine("Aucun prompt, chemin, identifiant de session ou secret copié.");
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
                display.Detail = "Relevé des tâches ancien ou indisponible";
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
                display.Detail = "Événement de début local · activité récente";
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
                display.Detail = "Début enregistré, mais activité non vérifiable";
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
                    display.Detail = "Dernier tour local finished, pas l'état global de Codex";
                    return display;
                }
                display.Detail = "Dernier tour enregistré : finished ; activité actuelle inconnue";
            }
            else
                display.Detail = "None exécution active confirmée in les journaux";
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

        private static bool SameTurn(LiveTask a, LiveTask b)
        {
            if (a == null || b == null || string.IsNullOrEmpty(a.SessionKey) ||
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
                if (!IsTerminal(task) || !task.FinishedUtc.HasValue ||
                    task.FinishedUtc.Value < task.StartedUtc ||
                    string.IsNullOrEmpty(task.SessionKey)) continue;
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
        {
            LiveTaskReport report = new LiveTaskReport { CheckedUtc = now };
            string root = SessionRoot();
            if (!Directory.Exists(root))
            {
                report.Message = "Unavailable · aucun dossier de sessions locales Codex";
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
                foreach (FileInfo info in candidates.OrderByDescending(f => f.LastWriteTimeUtc)
                    .Take(DiscoveryFiles))
                {
                    try
                    {
                        // Root session may have less recent disk activity than 12 subagents.
                        // Inspect metadata FIRST, then parse a bounded tail only for roots.
                        string sessionMeta = ReadFirstMetadataLine(info.FullName);
                        if (sessionMeta == null) { unknownHeaders++; continue; }
                        string threadKind = HeaderThreadKind(sessionMeta);
                        if (threadKind == "subagent") { ignoredChildren++; continue; }
                        if (threadKind != "user") { unknownHeaders++; continue; }
                        if (++checkedRoots > MaxFiles) break;
                        LiveTask task = ParseLines(
                            new string[] { sessionMeta }.Concat(ReadTail(info.FullName, TailBytes)),
                            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), now);
                        // A 10+ minute Desktop turn may generate more than 512 KiB
                        // of JSONL; the start event then falls outside the tail.
                        // Only expand for the freshest root, never every old file.
                        if (task == null && checkedRoots == 1 &&
                            info.LastWriteTimeUtc >= now.UtcDateTime.AddMinutes(-2))
                            task = ParseLines(new string[] { sessionMeta }.Concat(
                                ReadTail(info.FullName, ActiveRootFallbackBytes)),
                                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), now);
                        if (task != null) task.SessionKey = info.Name;
                        if (task != null && !task.IsRootConfirmed) continue;
                        if (task != null && (task.Phase == LiveTaskPhase.Active ||
                            (task.Phase == LiveTaskPhase.Unconfirmed &&
                            now - task.LastWriteUtc < TimeSpan.FromMinutes(30)) ||
                            now - (task.FinishedUtc ?? task.StartedUtc) < TimeSpan.FromHours(2)))
                            report.Tasks.Add(task);
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
                report.Message = "Tours principaux locaux · " + checkedRoots +
                    " inspecté(s), " + ignoredChildren + " sous-agent(s) ignoré(s)" +
                    (unknownHeaders > 0 ? " · " + unknownHeaders + " source(s) non classée(s)" : "");
                if (report.Tasks.Count == 0)
                    report.Message += " · tour actuel non vérifiable";
            }
            catch (UnauthorizedAccessException) { report.Message = "Unavailable · accès au dossier refusé"; }
            catch (IOException) { report.Message = "À vérifier · lecture des sessions interruptede"; }
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

        internal static LiveTask ParseLines(IEnumerable<string> lines, DateTimeOffset fileWriteUtc, DateTimeOffset now)
        {
            LiveTask last = null;
            System.Web.Script.Serialization.JavaScriptSerializer serializer =
                new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 1048576 };
            string workspace = "Project unknown";
            string originator = "Not provided";
            string threadSource = "Inconnue";
            bool isSubagent = false;
            bool isRoot = false;
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line) || line.Length > 1048576) continue;
                if (!Relevant(line)) continue;
                Dictionary<string, object> root;
                try { root = Json.Object(serializer.DeserializeObject(line)); }
                catch (ArgumentException) { continue; }
                if (root == null) continue;
                string type = Json.String(Json.Get(root, "type"));
                Dictionary<string, object> payload = Json.Object(Json.Get(root, "payload"));
                if (payload == null) continue;
                if (type == "session_meta")
                {
                    string cwd = Json.String(Json.Get(payload, "cwd"));
                    if (!string.IsNullOrEmpty(cwd))
                    {
                        string normalized = cwd.Replace('\\', '/').TrimEnd('/');
                        string name = normalized.Substring(normalized.LastIndexOf('/') + 1);
                        workspace = Clean(name, "Project unknown");
                    }
                    originator = Clean(Json.String(Json.Get(payload, "originator")), originator);
                    object threadSourceRaw = Json.Get(payload, "thread_source");
                    string kind = Json.String(threadSourceRaw as string);
                    Dictionary<string, object> kindObject = Json.Object(threadSourceRaw);
                    if (kindObject != null && kindObject.Count > 0)
                        kind = kindObject.Keys.FirstOrDefault();
                    threadSource = Clean(kind, threadSource);
                    string parentId = Json.String(Json.Get(payload, "parent_thread_id"));
                    isSubagent = !string.IsNullOrWhiteSpace(parentId) ||
                        threadSource.IndexOf("subagent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        threadSource.IndexOf("sub_agent", StringComparison.OrdinalIgnoreCase) >= 0;
                    isRoot = !isSubagent &&
                        string.Equals(threadSource, "user", StringComparison.OrdinalIgnoreCase);
                    if (last != null)
                    {
                        last.Workspace = workspace;
                        last.Originator = originator;
                        last.ThreadSource = threadSource;
                        last.IsSubagent = isSubagent;
                        last.IsRootConfirmed = isRoot;
                    }
                    continue;
                }
                if (type == "event_msg")
                {
                    string ev = Json.String(Json.Get(payload, "type"));
                    string turnId = Json.String(Json.Get(payload, "turn_id"));
                    DateTimeOffset eventTime = Timestamp(root, now);
                    if (ev == "task_started" || ev == "turn_started")
                    {
                        if (eventTime == DateTimeOffset.MinValue || eventTime > now.AddSeconds(10)) continue;
                        long? epoch = Json.Long(Json.Get(payload, "started_at"));
                        if (epoch.HasValue)
                        {
                            try
                            {
                                DateTimeOffset candidate = DateTimeOffset.FromUnixTimeSeconds(epoch.Value);
                                if (candidate <= now.AddSeconds(10) && candidate >= eventTime.AddMinutes(-5))
                                    eventTime = candidate;
                            }
                            catch (ArgumentOutOfRangeException) { }
                        }
                        last = new LiveTask { Id = Clean(turnId, ""), Workspace = workspace,
                            ThreadSource = threadSource, Originator = originator,
                            IsSubagent = isSubagent, IsRootConfirmed = isRoot,
                            StartedUtc = eventTime, Phase = LiveTaskPhase.Active, LastWriteUtc = fileWriteUtc };
                    }
                    else if (last != null && (string.IsNullOrEmpty(turnId) || turnId == last.Id))
                    {
                        if (ev == "task_complete" || ev == "turn_complete" || ev == "turn_completed")
                        {
                            last.Phase = Json.Get(payload, "error") != null ? LiveTaskPhase.Failed : LiveTaskPhase.Completed;
                            last.FinishedUtc = eventTime == DateTimeOffset.MinValue ? (DateTimeOffset?)null : eventTime;
                            long? duration = Json.Long(Json.Get(payload, "duration_ms"));
                            if (duration.HasValue && duration.Value >= 0 && duration.Value <= 86400000L &&
                                last.FinishedUtc.HasValue &&
                                Math.Abs(((last.FinishedUtc.Value - last.StartedUtc).TotalMilliseconds) -
                                    duration.Value) <= 120000)
                                last.RecordedDurationMs = duration;
                        }
                        else if (ev == "turn_aborted" || ev == "task_aborted")
                        {
                            last.Phase = LiveTaskPhase.Interrupted;
                            last.FinishedUtc = eventTime == DateTimeOffset.MinValue ? (DateTimeOffset?)null : eventTime;
                        }
                    }
                }
                else if (type == "turn_context" && last != null && last.Phase == LiveTaskPhase.Active)
                {
                    string turnId = Json.String(Json.Get(payload, "turn_id"));
                    if (!string.IsNullOrEmpty(turnId) && !string.IsNullOrEmpty(last.Id) && turnId != last.Id) continue;
                    last.Model = Clean(Json.String(Json.Get(payload, "model")), last.Model);
                    string effort = Json.String(Json.Get(payload, "effort")) ??
                        Json.String(Json.Get(payload, "reasoning_effort")) ??
                        Json.String(Json.Get(payload, "model_reasoning_effort"));
                    Dictionary<string, object> mode = Json.Object(Json.Get(payload, "collaboration_mode"));
                    Dictionary<string, object> settings = Json.Object(Json.Get(mode, "settings"));
                    if (effort == null) effort = Json.String(Json.Get(settings, "model_reasoning_effort"));
                    last.Effort = Clean(effort, last.Effort);
                }
            }
            if (last == null) return null;
            if (last.Phase == LiveTaskPhase.Active)
            {
                double age = (now - fileWriteUtc).TotalSeconds;
                if (age < -5 || age > ActivityMaxAgeSeconds) last.Phase = LiveTaskPhase.Unconfirmed;
            }
            if (last.FinishedUtc.HasValue && last.FinishedUtc.Value < last.StartedUtc)
            {
                last.Phase = LiveTaskPhase.Unconfirmed;
                last.FinishedUtc = null;
            }
            return last;
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
