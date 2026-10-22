using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        internal string ThreadId = ""; // opaque thread id from session_meta.payload.id
        internal string ParentThreadId = ""; // opaque parent id from session_meta
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
        internal DateTimeOffset ChildEvidenceUtc;
        internal bool RecentDirectEvidence;
        internal DateTimeOffset DirectEvidenceUtc;
        internal int ActiveChildCount;
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

    internal enum TaskDisplayKind { ObservedActive, DelegatedObserved, Uncertain, RecentTerminal, NotVerified }

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
        internal int RootSessionCandidates;
        internal int RootSessionsExcludedByCap;
        internal int ChildSessionCandidates;
        internal int ChildSessionsLinked;
        internal int ChildSessionsUnlinked;
        internal int ActiveChildSessions;
        internal int UnknownOrigins;
        internal long RecoveryBytesRead;
        internal long RecoveryProgressBytes;
        internal long RecentTailBytesRead;
        internal int RecoverySessions;
        internal int RecoveryLimitedSessions;
        internal int RecoveryLimitedBySize;
        internal int RecoveryLimitedByTimeout;
        internal int RecoveryLimitedByInvalidLine;
        internal int RecoveryLimitedByPartialRead;
        internal long RecoveryDurationMs;
        internal int ActiveCount { get { return Tasks.Count(t => t.IsRootConfirmed && !t.IsSubagent && t.Phase == LiveTaskPhase.Active); } }
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
            text.AppendLine("Root sessions: candidates=" + RootSessionCandidates +
                " ; examined=" + ExaminedRoots + " ; excluded by discovery cap=" + RootSessionsExcludedByCap +
                " ; unknown origins=" + UnknownOrigins);
            text.AppendLine("Child sessions: candidates=" + ChildSessionCandidates +
                " ; linked=" + ChildSessionsLinked + " ; unlinked or ambiguous=" + ChildSessionsUnlinked +
                " ; active and fresh=" + ActiveChildSessions);
            text.AppendLine("Recovery: " + RecoverySessions + " session(s), " + RecoveryBytesRead +
                " byte(s) this poll, cumulative progress="+RecoveryProgressBytes+" byte(s), recent-tail="+RecentTailBytesRead+" byte(s), " + RecoveryDurationMs + " ms, limited=" + RecoveryLimitedSessions +
                " ; reasons: size=" + RecoveryLimitedBySize + ", timeout=" + RecoveryLimitedByTimeout +
                ", invalid-line=" + RecoveryLimitedByInvalidLine + ", partial-read=" + RecoveryLimitedByPartialRead);
            TaskDisplay display = DisplayAt(now);
            string selectedType = display.Kind == TaskDisplayKind.ObservedActive ? "ROOT ACTIVE" :
                display.Kind == TaskDisplayKind.DelegatedObserved ? "ROOT DELEGATED" :
                display.Kind == TaskDisplayKind.Uncertain || display.Kind == TaskDisplayKind.NotVerified ? "UNVERIFIED" :
                display.Kind == TaskDisplayKind.RecentTerminal ? "ROOT RECENT TERMINAL" : "OTHER";
            string evidenceAge = "none";
            if (display.Primary != null)
            {
                DateTimeOffset proof = display.Kind == TaskDisplayKind.DelegatedObserved ? display.Primary.ChildEvidenceUtc : display.Primary.LastWriteUtc;
                if (proof != DateTimeOffset.MinValue) evidenceAge = Math.Max(0, (int)(now - proof).TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s";
            }
            text.AppendLine("Global mission selection: " + selectedType +
                " ; phase=" + (display.Primary == null ? "none" : display.Primary.Phase.ToString()) +
                " ; evidence-age=" + evidenceAge + " ; reason=" + SafeDiagnosticReason(display));
            text.AppendLine("Last primary evidence age (s): " + AgeSeconds(Tasks.Where(t => t.IsRootConfirmed && !t.IsSubagent).Select(t => t.LastWriteUtc), now));
            text.AppendLine("Last child evidence age (s): " + AgeSeconds(Tasks.Where(t => t.IsSubagent).Select(t => t.LastWriteUtc), now));
            List<LiveTask> roots = Tasks.Where(t => !t.IsSubagent).OrderByDescending(t => t.StartedUtc).Take(6).ToList();
            List<LiveTask> children = Tasks.Where(t => t.IsSubagent).OrderByDescending(t => t.StartedUtc).Take(6).ToList();
            AppendRecentTurns(text, "Last primary turns", roots, now);
            AppendRecentTurns(text, "Last child turns", children, now);
            List<LiveTask> sixRecent = Tasks.OrderByDescending(t => t.StartedUtc).Take(6).ToList();
            bool sixTerminal = sixRecent.Count == 6 && sixRecent.All(t => t.Phase == LiveTaskPhase.Completed ||
                t.Phase == LiveTaskPhase.Failed || t.Phase == LiveTaskPhase.Interrupted);
            text.AppendLine("Recent history vs global status: " + (sixTerminal ?
                "the six latest turns are terminal history; " : "latest turns do not determine global status; ") +
                "global selection comes from the selected mission's fresh confirmed root or linked-child evidence, otherwise it is UNVERIFIED.");
            text.AppendLine("No prompts, paths, session IDs or secrets copied.");
            return text.ToString();
        }

        private static string SafeDiagnosticReason(TaskDisplay display)
        {
            if (display.Kind == TaskDisplayKind.ObservedActive) return "fresh confirmed primary-session evidence";
            if (display.Kind == TaskDisplayKind.DelegatedObserved) return "fresh active child evidence linked by observed parent identifier; parent emission not observed";
            if (display.Kind == TaskDisplayKind.Uncertain) return "a recent start exists but activity is not confirmed";
            if (display.Kind == TaskDisplayKind.RecentTerminal) return "recent terminal is historical only";
            if (display.Detail == "Task reading stale or unavailable") return "reader snapshot stale or unavailable";
            return "no fresh confirmed root or linked-child evidence";
        }

        private static string SafeDiagnosticSource(string source)
        {
            if (string.Equals(source, "user", StringComparison.OrdinalIgnoreCase)) return "user";
            if (string.Equals(source, "subagent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "sub_agent", StringComparison.OrdinalIgnoreCase)) return "subagent";
            if (string.Equals(source, "guardian_review", StringComparison.OrdinalIgnoreCase)) return "guardian_review";
            if (string.Equals(source, "system", StringComparison.OrdinalIgnoreCase)) return "system";
            return "other";
        }

        private static string AgeSeconds(IEnumerable<DateTimeOffset> values, DateTimeOffset now)
        {
            DateTimeOffset value = values.Where(t => t != DateTimeOffset.MinValue).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
            return value == DateTimeOffset.MinValue ? "none" : Math.Max(0, (int)(now - value).TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        private static void AppendRecentTurns(StringBuilder text, string title, List<LiveTask> turns, DateTimeOffset now)
        {
            text.AppendLine(title + " (up to 6):");
            if (turns.Count == 0) { text.AppendLine("  none"); return; }
            int i = 0;
            foreach (LiveTask task in turns)
            {
                // Only allowlisted source categories leave this report; no session IDs, paths, prompts or tokens.
                text.AppendLine("  Turn #" + (++i) + " : " +
                    task.Phase + " ; source=" + SafeDiagnosticSource(task.ThreadSource) +
                    " ; started (UTC)=" + task.StartedUtc.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture) +
                    " ; completed (UTC)=" +
                    (task.FinishedUtc.HasValue ?
                        task.FinishedUtc.Value.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "absent") +
                    " ; duration=" + task.Duration(now) +
                    " ; duration_ms=" + (task.RecordedDurationMs.HasValue ?
                        task.RecordedDurationMs.Value.ToString(CultureInfo.InvariantCulture) : "missing") +
                    " ; freshness (s)=" + Math.Max(0, (int)(now - task.LastWriteUtc).TotalSeconds));
            }
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
                t.IsRootConfirmed && !t.IsSubagent &&
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

            LiveTask delegated = Tasks.Where(t => t.IsRootConfirmed && !t.IsSubagent && !t.FinishedUtc.HasValue &&
                !t.IdentityUncertain && !string.IsNullOrEmpty(t.ThreadId) && t.ActiveChildCount > 0 &&
                t.ChildEvidenceUtc != DateTimeOffset.MinValue &&
                (now-t.ChildEvidenceUtc).TotalSeconds >= -5 &&
                (now-t.ChildEvidenceUtc).TotalSeconds <= LiveTaskReader.ActivityMaxAgeSeconds)
                .OrderByDescending(t => t.ChildEvidenceUtc).FirstOrDefault();
            if (delegated != null)
            {
                display.Primary = delegated;
                display.Kind = TaskDisplayKind.DelegatedObserved;
                display.Detail = "Linked child thread · recent activity; parent emission not observed";
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
                if (!task.IsRootConfirmed || task.IsSubagent || task.IdentityUncertain || !IsTerminal(task) || !task.FinishedUtc.HasValue ||
                    task.FinishedUtc.Value < task.StartedUtc ||
                    string.IsNullOrEmpty(task.SessionKey) ||
                    // An old terminal event is history, not a new notification.
                    task.FinishedUtc.Value < previous.CheckedUtc.AddSeconds(-5) ||
                    task.FinishedUtc.Value > current.CheckedUtc.AddSeconds(5)) continue;
                bool wasActive = previous.Tasks.Any(old => old.Phase == LiveTaskPhase.Active &&
                    SameTurn(old, task));
                bool wasAlreadySeen = previous.Tasks.Any(old => SameTurn(old, task) ||
                    (!string.IsNullOrEmpty(old.SessionKey) && string.Equals(old.SessionKey,task.SessionKey,StringComparison.Ordinal) &&
                     old.StartedUtc==task.StartedUtc && (string.IsNullOrEmpty(old.Id)||string.IsNullOrEmpty(task.Id)||old.Id==task.Id)));
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
        private const long RecentTailMaxBytesPerRead = 8388608;
        internal const int RecoveryMaxBytes = 67108864;
        internal const int RecoveryMaxBytesPerRead = 33554432;
        internal const int RecoverySliceBytes = 2097152;
        internal const long RecoveryMaxBytesPerSource = 67108864;
        internal const int RecoveryMaxLineBytes = 1048576;
        private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(4);
        private const int MaxFiles = 12;
        private const int DiscoveryFiles = 48;
        internal const int MaxTurnsPerSession = 64;
        private sealed class SessionCache
        {
            internal long Length;
            internal DateTime Write;
            internal List<LiveTask> Turns;
            internal string ThreadId;
            internal string ParentThreadId;
            internal string FileIdentity;
            internal long RecoveryOffset;
            internal long RecoveryBytesScanned;
            internal bool SkippingOversizedLine;
            internal bool IsRoot;
            internal bool IsSubagent;
            internal bool Complete;
            internal bool LimitedBySize;
            internal bool LimitedByTimeout;
            internal bool LimitedByInvalidLine;
            internal bool LimitedByPartialRead;
        }
        private sealed class ChildCandidate
        {
            internal string ParentThreadId;
            internal bool ActiveAndFresh;
        }
        private sealed class BoundedScan
        {
            internal readonly List<string> Lines = new List<string>();
            internal long BytesRead;
            internal bool Complete;
            internal bool TimedOut;
            internal bool OversizedLine;
            internal bool InvalidLine;
            internal bool LimitedBySize;
            internal bool PartialRead;
            internal long StartOffset;
            internal long NextOffset;
            internal string FileIdentity;
            internal bool SkippingOversizedLine;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            internal uint FileAttributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            internal uint VolumeSerialNumber;
            internal uint FileSizeHigh;
            internal uint FileSizeLow;
            internal uint NumberOfLinks;
            internal uint FileIndexHigh;
            internal uint FileIndexLow;
        }
        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle,out ByHandleFileInformation info);
        private static readonly Dictionary<string,SessionCache> Sessions = new Dictionary<string,SessionCache>(StringComparer.OrdinalIgnoreCase);
        private static readonly object ReadGate = new object();
        private static int childRecoveryCursor;
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
        internal static void ClearCacheForTests()
        { lock(ReadGate){Sessions.Clear();HeaderCache.Clear();} }
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
                int observedChildren = 0;
                int unknownHeaders = 0;
                int excludedRoots = 0;
                List<string> rootCandidateIds = new List<string>();
                List<ChildCandidate> childCandidates = new List<ChildCandidate>();
                Stopwatch recoveryClock = Stopwatch.StartNew();
                List<LiveTask> discovered = new List<LiveTask>();
                long recentTailBudget=RecentTailMaxBytesPerRead;
                List<FileInfo> stableCandidates=candidates.OrderByDescending(f=>f.LastWriteTimeUtc).Take(DiscoveryFiles).ToList();
                HashSet<string> admittedRootPaths=new HashSet<string>(stableCandidates.Where(f=>
                    HeaderThreadKind(ReadFirstMetadataLine(f.FullName)??"")=="user").Take(MaxFiles).Select(f=>f.FullName),StringComparer.OrdinalIgnoreCase);
                List<FileInfo> rootsForRecovery=stableCandidates.Where(f=>admittedRootPaths.Contains(f.FullName)).ToList();
                List<FileInfo> childrenForRecovery=stableCandidates.Where(f=>
                    HeaderThreadKind(ReadFirstMetadataLine(f.FullName)??"")=="subagent").ToList();
                List<FileInfo> orderedCandidates=new List<FileInfo>(rootsForRecovery);
                if(childrenForRecovery.Count>0) {
                    int remainingRecoverySlots=Math.Max(0,(RecoveryMaxBytesPerRead-rootsForRecovery.Count*RecoverySliceBytes)/RecoverySliceBytes);
                    int rotate=((childRecoveryCursor%childrenForRecovery.Count)+childrenForRecovery.Count)%childrenForRecovery.Count;
                    orderedCandidates.AddRange(childrenForRecovery.Skip(rotate).Concat(childrenForRecovery.Take(rotate)));
                    childRecoveryCursor=(rotate+Math.Max(1,remainingRecoverySlots))%childrenForRecovery.Count;
                }
                orderedCandidates.AddRange(stableCandidates.Where(f=>!admittedRootPaths.Contains(f.FullName)&&
                    !childrenForRecovery.Any(c=>string.Equals(c.FullName,f.FullName,StringComparison.OrdinalIgnoreCase))));
                foreach (FileInfo info in orderedCandidates)
                {
                    try
                    {
                        string currentFileIdentity=GetFileIdentity(info.FullName);
                        SessionCache priorSource;
                        if (Sessions.TryGetValue(info.FullName,out priorSource) &&
                            (info.Length<priorSource.Length || string.IsNullOrEmpty(currentFileIdentity) ||
                             !string.Equals(priorSource.FileIdentity,currentFileIdentity,StringComparison.Ordinal)))
                            HeaderCache.Remove(info.FullName);
                        string sessionMeta = ReadFirstMetadataLine(info.FullName);
                        if (sessionMeta == null) { unknownHeaders++; continue; }
                        string threadKind = HeaderThreadKind(sessionMeta);
                        if (threadKind == "unknown") { unknownHeaders++; continue; }
                        bool isChild=threadKind=="subagent";
                        if (isChild) {
                            observedChildren++;
                            childCandidates.Add(new ChildCandidate { ParentThreadId=HeaderParentThreadId(sessionMeta) });
                            if(observedChildren>DiscoveryFiles)continue;
                        }
                        else if (threadKind=="user") {
                            report.RootSessionCandidates++;
                            string candidateId=HeaderThreadId(sessionMeta);
                            if(!string.IsNullOrEmpty(candidateId))rootCandidateIds.Add(candidateId);
                            if(!admittedRootPaths.Contains(info.FullName)){excludedRoots++;continue;}
                            checkedRoots++;
                        }
                        else { unknownHeaders++; continue; }
                        SessionCache cache;
                        bool known = Sessions.TryGetValue(info.FullName,out cache);
                        BoundedScan recoveryScan=null;
                        List<LiveTask> turns;
                        bool cacheResult=true;
                        bool sourceComplete=true;
                        bool checkpointValid=known && !string.IsNullOrEmpty(currentFileIdentity) &&
                            string.Equals(cache.FileIdentity,currentFileIdentity,StringComparison.Ordinal) && info.Length>=cache.Length;
                        if (known && cache.Complete && cache.Length == info.Length && cache.Write == info.LastWriteTimeUtc)
                        {
                            turns = cache.Turns.Select(t => t.Copy()).ToList();
                            sourceComplete=cache.Complete;
                            if(!cache.Complete)foreach(LiveTask open in turns.Where(t=>!t.FinishedUtc.HasValue))Uncertain(open);
                            if(!cache.Complete){report.RecoveryLimitedSessions++;AddRecoveryReasons(report,cache);}
                        }
                        else {
                            bool appendOnly=checkpointValid && info.Length>cache.Length && cache.ThreadId==HeaderThreadId(sessionMeta) &&
                                cache.ParentThreadId==HeaderParentThreadId(sessionMeta) && cache.Complete;
                            if(appendOnly) {
                                long tailBytes=0;IEnumerable<string> tailLines=recentTailBudget>0?ReadTail(info.FullName,(int)Math.Min(TailBytes,recentTailBudget),out tailBytes):Enumerable.Empty<string>();
                                recentTailBudget=Math.Max(0,recentTailBudget-tailBytes);report.RecentTailBytesRead+=tailBytes;
                                turns=ParseCollection(new [] {sessionMeta}.Concat(tailLines),new DateTimeOffset(info.LastWriteTimeUtc,TimeSpan.Zero),now,cache.Turns);
                            }
                            else {
                                BoundedScan scan=new BoundedScan();
                                recoveryScan=scan;
                                long remaining=Math.Max(0,RecoveryMaxBytesPerRead-report.RecoveryBytesRead);
                                if(remaining<=0){scan.LimitedBySize=true;scan.PartialRead=info.Length>0;}
                                if(recoveryClock.Elapsed>RecoveryTimeout){scan.TimedOut=true;scan.PartialRead=info.Length>0;}
                                if(string.IsNullOrEmpty(currentFileIdentity)){scan.PartialRead=info.Length>0;remaining=0;}
                                long startOffset=checkpointValid && cache.ThreadId==HeaderThreadId(sessionMeta) &&
                                    cache.ParentThreadId==HeaderParentThreadId(sessionMeta) ? cache.RecoveryOffset : 0;
                                long sourceBytes=checkpointValid?cache.RecoveryBytesScanned:0;
                                long sourceRemaining=Math.Max(0,RecoveryMaxBytesPerSource-sourceBytes);
                                scan.InvalidLine=checkpointValid&&cache.LimitedByInvalidLine;
                                scan.PartialRead=info.Length>0;
                                if(sourceRemaining==0){scan.LimitedBySize=true;scan.PartialRead=true;}
                                List<LiveTask> priorTurns=startOffset>0 ? cache.Turns : null;
                                IEnumerable<string> lines=remaining>0&&sourceRemaining>0&&recoveryClock.Elapsed<=RecoveryTimeout ?
                                    ReadBoundedPrefix(info.FullName,scan,(int)Math.Min(RecoverySliceBytes,Math.Min(remaining,sourceRemaining)),recoveryClock,startOffset,
                                        startOffset>0&&checkpointValid&&cache.SkippingOversizedLine) : Enumerable.Empty<string>();
                                turns=ParseCollection(new [] {sessionMeta}.Concat(lines),
                                    new DateTimeOffset(info.LastWriteTimeUtc,TimeSpan.Zero),now,priorTurns,scan);
                                sourceComplete=scan.Complete;
                                report.RecoverySessions++;
                                report.RecoveryBytesRead+=scan.BytesRead;
                                if(!scan.Complete){report.RecoveryLimitedSessions++;AddRecoveryReasons(report,scan);cacheResult=false;}
                                if(!scan.Complete && scan.BytesRead>0)cacheResult=true;
                                if(!scan.Complete && scan.BytesRead==0 && info.Length>0)cacheResult=false;
                            }
                            if(cacheResult) {
                                if (Sessions.Count >= 64 && !known) Sessions.Clear();
                                Sessions[info.FullName] = new SessionCache { Length = info.Length, Write = info.LastWriteTimeUtc,
                                Turns = turns.Select(t => t.Copy()).ToList(), ThreadId=HeaderThreadId(sessionMeta),
                                ParentThreadId=HeaderParentThreadId(sessionMeta),IsRoot=threadKind=="user",IsSubagent=isChild,
                                FileIdentity=currentFileIdentity,RecoveryOffset=recoveryScan==null?(sourceComplete?info.Length:cache.RecoveryOffset):recoveryScan.NextOffset,
                                RecoveryBytesScanned=(checkpointValid?cache.RecoveryBytesScanned:0)+(recoveryScan==null?0:recoveryScan.BytesRead),
                                SkippingOversizedLine=recoveryScan!=null&&recoveryScan.SkippingOversizedLine,
                                Complete=sourceComplete,LimitedBySize=(checkpointValid&&cache.LimitedBySize)||(recoveryScan!=null&&recoveryScan.LimitedBySize),
                                LimitedByTimeout=recoveryScan!=null&&recoveryScan.TimedOut,
                                LimitedByInvalidLine=(checkpointValid&&cache.LimitedByInvalidLine)||(recoveryScan!=null&&recoveryScan.InvalidLine),
                                LimitedByPartialRead=(checkpointValid&&cache.LimitedByPartialRead)||(recoveryScan!=null&&recoveryScan.PartialRead) };
                            }
                        }
                        if(known||cacheResult) {
                            SessionCache progress;
                            if(Sessions.TryGetValue(info.FullName,out progress))report.RecoveryProgressBytes+=progress.RecoveryBytesScanned;
                        }
                        if(!sourceComplete) {
                            // Historical prefix recovery and current activity observation are
                            // separate bounded reads. A recent task is promoted only when its
                            // start identity and a timestamped direct event are present in the
                            // tail; file growth/mtime alone never qualifies.
                            List<LiveTask> historical=turns;
                            List<LiveTask> seed=cache!=null&&cache.Turns!=null?cache.Turns.Select(t=>{LiveTask copy=t.Copy();copy.RecentDirectEvidence=false;return copy;}).ToList():new List<LiveTask>();
                            long tailBytes=0;IEnumerable<string> tailLines=recentTailBudget>0?ReadTail(info.FullName,(int)Math.Min(TailBytes,recentTailBudget),out tailBytes):Enumerable.Empty<string>();
                            recentTailBudget=Math.Max(0,recentTailBudget-tailBytes);report.RecentTailBytesRead+=tailBytes;
                            List<LiveTask> tail=ParseCollection(new [] {sessionMeta}.Concat(tailLines),new DateTimeOffset(info.LastWriteTimeUtc,TimeSpan.Zero),now,seed);
                            foreach(LiveTask open in historical)Uncertain(open);
                            foreach(LiveTask recent in tail.Where(t=>!t.IdentityUncertain&&t.Id.Length>0&&t.RecentDirectEvidence&&
                                t.StartedUtc<=now.AddSeconds(5)&&(now-t.DirectEvidenceUtc).TotalSeconds>=-5&&
                                (now-t.DirectEvidenceUtc).TotalSeconds<=ActivityMaxAgeSeconds&&
                                ((!t.FinishedUtc.HasValue&&t.Phase==LiveTaskPhase.Active)||
                                 (t.FinishedUtc.HasValue&&t.FinishedUtc.Value>=now.AddSeconds(-ActivityMaxAgeSeconds))))) {
                                LiveTask prior=historical.FirstOrDefault(t=>t.Id==recent.Id);
                                if(prior!=null) {
                                    prior.IdentityUncertain=false;prior.Phase=recent.Phase;prior.FinishedUtc=recent.FinishedUtc;
                                    prior.LastWriteUtc=recent.LastWriteUtc;prior.RecordedDurationMs=recent.RecordedDurationMs;
                                    prior.StartedUtc=recent.StartedUtc;prior.RecentDirectEvidence=true;prior.DirectEvidenceUtc=recent.DirectEvidenceUtc;
                                } else historical.Add(recent.Copy());
                            }
                        }
                        foreach (LiveTask task in turns) {
                            task.SessionKey = info.Name;
                            Expire(task,now);
                            if(task.IsRootConfirmed && !task.IsSubagent) {
                                if (task.Phase == LiveTaskPhase.Active ||
                                    (task.Phase == LiveTaskPhase.Unconfirmed && now-task.LastWriteUtc < TimeSpan.FromMinutes(30)) ||
                                    now-(task.FinishedUtc ?? task.StartedUtc) < TimeSpan.FromHours(2)) discovered.Add(task);
                            }
                            else if(task.IsSubagent && (task.Phase==LiveTaskPhase.Active ||
                                (task.Phase==LiveTaskPhase.Unconfirmed && now-task.LastWriteUtc<TimeSpan.FromMinutes(30)) ||
                                now-(task.FinishedUtc ?? task.StartedUtc)<TimeSpan.FromHours(2))) discovered.Add(task);
                        }
                        if(isChild) {
                            ChildCandidate candidate=childCandidates[childCandidates.Count-1];
                            candidate.ActiveAndFresh=turns.Any(t=>t.IsSubagent&&t.IsRootConfirmed==false&&t.Phase==LiveTaskPhase.Active&&
                                !t.IdentityUncertain&&
                                t.StartedUtc<=now.AddSeconds(5)&&(now-t.LastWriteUtc).TotalSeconds>=-5&&
                                (now-t.LastWriteUtc).TotalSeconds<=ActivityMaxAgeSeconds);
                        }
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (ArgumentException) { }
                }
                report.RecoveryDurationMs=(long)recoveryClock.Elapsed.TotalMilliseconds;
                report.ExaminedRoots=checkedRoots;
                report.RootSessionsExcludedByCap=excludedRoots;
                report.ChildSessionCandidates=observedChildren;
                foreach(ChildCandidate child in childCandidates) {
                    int matches=string.IsNullOrEmpty(child.ParentThreadId)?0:rootCandidateIds.Count(id=>id==child.ParentThreadId);
                    if(matches==1)report.ChildSessionsLinked++;else report.ChildSessionsUnlinked++;
                    if(child.ActiveAndFresh)report.ActiveChildSessions++;
                }
                LinkDelegatedEvidence(discovered,now);
                report.Tasks.AddRange(discovered);
                report.Tasks.Sort(delegate(LiveTask x, LiveTask y) {
                    return y.LastWriteUtc.CompareTo(x.LastWriteUtc);
                });
                report.IgnoredSubagents = observedChildren;
                report.UnknownOrigins = unknownHeaders;
                report.Message = "Local root turns · " + checkedRoots +
                    " checked, " + observedChildren + " linked/unlinked child files inspected" +
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

        private static string HeaderThreadId(string sanitizedMeta)
        { return HeaderIdentifier(sanitizedMeta,"id"); }
        private static string HeaderParentThreadId(string sanitizedMeta)
        { return HeaderIdentifier(sanitizedMeta,"parent_thread_id"); }
        private static string HeaderIdentifier(string sanitizedMeta,string field)
        {
            try {
                Dictionary<string,object> root=Json.Object(new System.Web.Script.Serialization.JavaScriptSerializer {MaxJsonLength=1048576}.DeserializeObject(sanitizedMeta));
                Dictionary<string,object> payload=Json.Object(Json.Get(root,"payload"));
                return SafeIdentifier(Json.String(Json.Get(payload,field)));
            } catch(ArgumentException) { return ""; }
        }
        private static string SafeIdentifier(string value)
        {
            if(string.IsNullOrEmpty(value) || value.Length>128)return "";
            foreach(char c in value)if(!(char.IsLetterOrDigit(c)||c=='-'||c=='_'))return "";
            return value;
        }

        private static void LinkDelegatedEvidence(List<LiveTask> tasks,DateTimeOffset now)
        {
            List<LiveTask> roots=tasks.Where(t=>t.IsRootConfirmed&&!t.IsSubagent&&!t.FinishedUtc.HasValue&&!string.IsNullOrEmpty(t.ThreadId)).ToList();
            List<LiveTask> children=tasks.Where(t=>t.IsSubagent&&!string.IsNullOrEmpty(t.ParentThreadId)).ToList();
            foreach(IGrouping<string,LiveTask> group in roots.GroupBy(t=>t.ThreadId,StringComparer.Ordinal)) {
                if(group.Count()!=1)continue;
                LiveTask root=group.First();
                if(root.IdentityUncertain)continue;
                List<LiveTask> open=roots.Where(t=>t.ThreadId==root.ThreadId&&!t.FinishedUtc.HasValue).ToList();
                if(open.Count!=1)continue;
                foreach(IGrouping<string,LiveTask> childGroup in children.Where(c=>c.ParentThreadId==root.ThreadId &&
                    c.Phase==LiveTaskPhase.Active&&!c.FinishedUtc.HasValue&&!c.IdentityUncertain&&
                    c.StartedUtc<=now.AddSeconds(5)&&(now-c.LastWriteUtc).TotalSeconds>=-5&&
                    (now-c.LastWriteUtc).TotalSeconds<=ActivityMaxAgeSeconds).GroupBy(c=>c.SessionKey,StringComparer.Ordinal)) {
                    if(childGroup.Count()!=1)continue;
                    LiveTask child=childGroup.First();root.ActiveChildCount++;
                    if(child.LastWriteUtc>root.ChildEvidenceUtc)root.ChildEvidenceUtc=child.LastWriteUtc;
                }
            }
        }

        private static void AddRecoveryReasons(LiveTaskReport report,SessionCache cache)
        {
            if(cache.LimitedBySize)report.RecoveryLimitedBySize++;
            if(cache.LimitedByTimeout)report.RecoveryLimitedByTimeout++;
            if(cache.LimitedByInvalidLine)report.RecoveryLimitedByInvalidLine++;
            if(cache.LimitedByPartialRead)report.RecoveryLimitedByPartialRead++;
        }
        private static void AddRecoveryReasons(LiveTaskReport report,BoundedScan scan)
        {
            if(scan.LimitedBySize)report.RecoveryLimitedBySize++;
            if(scan.TimedOut)report.RecoveryLimitedByTimeout++;
            if(scan.InvalidLine)report.RecoveryLimitedByInvalidLine++;
            if(scan.PartialRead)report.RecoveryLimitedByPartialRead++;
        }

        private static string GetFileIdentity(string path)
        {
            try {
                using(FileStream stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                    ByHandleFileInformation value;
                    if(!GetFileInformationByHandle(stream.SafeFileHandle,out value))return "";
                    return value.VolumeSerialNumber.ToString("X8",CultureInfo.InvariantCulture)+":"+
                        value.FileIndexHigh.ToString("X8",CultureInfo.InvariantCulture)+value.FileIndexLow.ToString("X8",CultureInfo.InvariantCulture);
                }
            } catch(IOException) { return ""; } catch(UnauthorizedAccessException) { return ""; }
        }

        private static IEnumerable<string> ReadBoundedPrefix(string path,BoundedScan scan,int maxBytes,Stopwatch recoveryClock,long startOffset,bool resumeOversized)
        {
            using(FileStream stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                long initialLength=stream.Length;
                scan.StartOffset=startOffset;scan.FileIdentity=GetFileIdentity(path);
                stream.Seek(startOffset,SeekOrigin.Begin);
                scan.LimitedBySize=false;
                byte[] buffer=new byte[8192];
                List<byte> line=new List<byte>(4096);
                bool oversized=resumeOversized;
                if(resumeOversized){scan.InvalidLine=true;scan.OversizedLine=true;}
                long lastCompleteOffset=startOffset;
                while(scan.BytesRead<maxBytes && stream.Position<initialLength) {
                    if(recoveryClock.Elapsed>RecoveryTimeout){scan.TimedOut=true;break;}
                    int count=(int)Math.Min(buffer.Length,Math.Min(maxBytes-scan.BytesRead,initialLength-stream.Position));
                    int read=stream.Read(buffer,0,count);if(read<=0)break;scan.BytesRead+=read;
                    for(int i=0;i<read;i++) {
                        byte b=buffer[i];
                        if(b==10) {
                            if(!oversized) {
                                string record;
                                try {record=new UTF8Encoding(false,true).GetString(line.ToArray());}
                                catch(DecoderFallbackException){record=null;scan.OversizedLine=true;scan.InvalidLine=true;}
                                if(record!=null){if(record.Length>0&&record[record.Length-1]=='\r')record=record.Substring(0,record.Length-1);if(Relevant(record))yield return record;}
                            }
                            line.Clear();oversized=false;lastCompleteOffset=startOffset+scan.BytesRead-(read-i-1);continue;
                        }
                        if(b==13)continue;
                        if(!oversized) { if(line.Count<RecoveryMaxLineBytes)line.Add(b); else {line.Clear();oversized=true;scan.OversizedLine=true;scan.InvalidLine=true;} }
                    }
                }
                scan.NextOffset=oversized?stream.Position:lastCompleteOffset;
                scan.SkippingOversizedLine=oversized;
                scan.PartialRead=stream.Position<initialLength;
                scan.Complete=stream.Position>=initialLength&&line.Count==0&&!oversized&&!scan.TimedOut&&!scan.OversizedLine;
            }
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
                    foreach (string field in new [] { "id", "thread_source", "originator", "parent_thread_id", "cwd" })
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

        private static IEnumerable<string> ReadTail(string path, int maxBytes,out long bytesRead)
        {
            bytesRead=0;
            List<string> records = new List<string>();
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                long snapshotLength=stream.Length;
                long start = Math.Max(0, snapshotLength - maxBytes);
                int count=(int)Math.Min(int.MaxValue,snapshotLength-start);
                byte[] bytes=new byte[count];stream.Seek(start,SeekOrigin.Begin);
                while(bytesRead<count) {int read=stream.Read(bytes,(int)bytesRead,count-(int)bytesRead);if(read<=0)break;bytesRead+=read;}
                using (MemoryStream bounded=new MemoryStream(bytes,0,(int)bytesRead,false))
                using (StreamReader reader = new StreamReader(bounded, new UTF8Encoding(false), true, 4096))
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
        { return ParseCollection(lines,fileWriteUtc,now,seed,null); }
        private static List<LiveTask> ParseCollection(IEnumerable<string> lines, DateTimeOffset fileWriteUtc, DateTimeOffset now, IEnumerable<LiveTask> seed, BoundedScan diagnosticScan)
        {
            List<LiveTask> turns = seed == null ? new List<LiveTask>() : seed.Where(t =>
                t.FinishedUtc.HasValue ? now-t.FinishedUtc.Value < TimeSpan.FromHours(2) : now-t.LastWriteUtc < TimeSpan.FromMinutes(30)).Select(t => t.Copy()).ToList();
            System.Web.Script.Serialization.JavaScriptSerializer serializer = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 1048576 };
            string workspace = "Project not provided", originator = "Not provided", threadSource = "Unknown", threadId="", parentThreadId="";
            bool isSubagent = false, isRoot = false;
            foreach (string line in lines) {
                if (string.IsNullOrEmpty(line) || line.Length > 1048576 || !Relevant(line)) continue;
                Dictionary<string,object> root;
                try { root = Json.Object(serializer.DeserializeObject(line)); } catch (ArgumentException) { if(diagnosticScan!=null)diagnosticScan.InvalidLine=true; continue; }
                if (root == null) { if(diagnosticScan!=null)diagnosticScan.InvalidLine=true; continue; }
                string type = Json.String(Json.Get(root,"type"));
                Dictionary<string,object> payload = Json.Object(Json.Get(root,"payload")); if (payload == null) continue;
                if (type == "session_meta") {
                    string cwd = Json.String(Json.Get(payload,"cwd"));
                    if (!string.IsNullOrEmpty(cwd)) { string path = cwd.Replace('\\','/').TrimEnd('/'); workspace = Clean(path.Substring(path.LastIndexOf('/')+1),workspace); }
                    originator = Clean(Json.String(Json.Get(payload,"originator")),originator);
                    threadId=SafeIdentifier(Json.String(Json.Get(payload,"id")));
                    parentThreadId=SafeIdentifier(Json.String(Json.Get(payload,"parent_thread_id")));
                    object raw = Json.Get(payload,"thread_source");
                    string kind = raw as string; Dictionary<string,object> kindObject = Json.Object(raw);
                    if (kindObject != null && kindObject.Count > 0) kind = kindObject.Keys.FirstOrDefault();
                    threadSource = Clean(kind,threadSource);
                    isSubagent = !string.IsNullOrWhiteSpace(Json.String(Json.Get(payload,"parent_thread_id"))) ||
                        threadSource.IndexOf("subagent",StringComparison.OrdinalIgnoreCase) >= 0 || threadSource.IndexOf("sub_agent",StringComparison.OrdinalIgnoreCase) >= 0;
                    isRoot = !isSubagent && string.Equals(threadSource,"user",StringComparison.OrdinalIgnoreCase);
                    foreach (LiveTask t in turns) { t.Workspace=workspace; t.Originator=originator; t.ThreadSource=threadSource; t.ThreadId=threadId; t.ParentThreadId=parentThreadId; t.IsSubagent=isSubagent; t.IsRootConfirmed=isRoot; }
                    continue;
                }
                string ev = type == "event_msg" ? Json.String(Json.Get(payload,"type")) : "";
                object idValue=Json.Get(payload,"turn_id");
                bool invalidIdType=idValue != null && !(idValue is string);
                string rawId = idValue as string;
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
                        else { existing.RecentDirectEvidence=true;existing.DirectEvidenceUtc=time; if(time>existing.LastWriteUtc)existing.LastWriteUtc=time; }
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
                    turns.Add(new LiveTask { Id=id, ThreadId=threadId, ParentThreadId=parentThreadId, Workspace=workspace, Originator=originator, ThreadSource=threadSource,
                        IsSubagent=isSubagent, IsRootConfirmed=isRoot, StartedUtc=time, LastWriteUtc=time,
                        Phase=LiveTaskPhase.Active, RecentDirectEvidence=true,DirectEvidenceUtc=time, IdentityUncertain=invalidIdType || (!string.IsNullOrEmpty(rawId) && id.Length == 0) });
                    continue;
                }
                if (invalidIdType || (!string.IsNullOrEmpty(rawId) && id.Length == 0)) { foreach (LiveTask t in turns.Where(t => !t.FinishedUtc.HasValue)) Uncertain(t); continue; }
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
                    task.Effort=Clean(effort,task.Effort); task.LastWriteUtc = time > task.LastWriteUtc ? time : task.LastWriteUtc; task.Phase=LiveTaskPhase.Active;task.RecentDirectEvidence=true;task.DirectEvidenceUtc=time;
                }
                else if (ev == "task_complete" || ev == "turn_complete" || ev == "turn_completed" || ev == "turn_aborted" || ev == "task_aborted") {
                    LiveTaskPhase phase = ev == "turn_aborted" || ev == "task_aborted" ? LiveTaskPhase.Interrupted : Json.Get(payload,"error") != null ? LiveTaskPhase.Failed : LiveTaskPhase.Completed;
                    if (time < task.StartedUtc || (task.FinishedUtc.HasValue && (task.FinishedUtc != time || task.Phase != phase))) { Uncertain(task); continue; }
                    task.RecentDirectEvidence=true;task.DirectEvidenceUtc=time;
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

