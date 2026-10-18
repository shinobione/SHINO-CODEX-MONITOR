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
        internal string Workspace = "Projet non communiqué";
        internal string Model = "Non communiqué";
        internal string Effort = "Non communiqué";
        internal LiveTaskPhase Phase;
        internal DateTimeOffset StartedUtc;
        internal DateTimeOffset? FinishedUtc;
        internal DateTimeOffset LastWriteUtc;
        internal string Source = "Codex local / sessions JSONL (non documenté)";
        internal string LastAction = "Activité non détaillée";

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
                    case LiveTaskPhase.Unconfirmed: return "À VÉRIFIER";
                    default: return "INDISPONIBLE";
                }
            }
        }

        internal string Duration(DateTimeOffset now)
        {
            DateTimeOffset end = FinishedUtc ?? (Phase == LiveTaskPhase.Active ? now : LastWriteUtc);
            if (end < StartedUtc) return "—";
            TimeSpan span = end - StartedUtc;
            if (span.TotalDays >= 1) return ((int)span.TotalDays) + " j " + span.Hours + " h";
            if (span.TotalHours >= 1) return ((int)span.TotalHours) + " h " + span.Minutes.ToString("00") + " min";
            return (Phase == LiveTaskPhase.Unconfirmed ? "≥ " : "") +
                ((int)span.TotalMinutes).ToString("00") + ":" + span.Seconds.ToString("00");
        }
    }

    internal sealed class LiveTaskReport
    {
        internal readonly List<LiveTask> Tasks = new List<LiveTask>();
        internal string Message = "Aucune session Codex locale récente";
        internal DateTimeOffset CheckedUtc;
        internal int ActiveCount { get { return Tasks.Count(t => t.Phase == LiveTaskPhase.Active); } }
        internal LiveTask Featured
        {
            get
            {
                return Tasks.OrderBy(t => t.Phase == LiveTaskPhase.Active ? 0 :
                    t.Phase == LiveTaskPhase.Unconfirmed ? 1 : 2)
                    .ThenByDescending(t => t.FinishedUtc ?? t.StartedUtc).FirstOrDefault();
            }
        }
    }

    internal static class LiveTaskReader
    {
        internal const int ActivityMaxAgeSeconds = 90;
        private const int TailBytes = 524288;
        private const int MaxFiles = 12;

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
                report.Message = "Indisponible · aucun dossier de sessions locales Codex";
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
                foreach (FileInfo info in candidates.OrderByDescending(f => f.LastWriteTimeUtc).Take(MaxFiles))
                {
                    try
                    {
                        LiveTask task = ParseLines(ReadTail(info.FullName),
                            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), now);
                        if (task != null && (task.Phase == LiveTaskPhase.Active ||
                            task.Phase == LiveTaskPhase.Unconfirmed ||
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
                if (report.Tasks.Count > 0)
                    report.Message = "Sessions locales · lecture seule · statut indicatif";
            }
            catch (UnauthorizedAccessException) { report.Message = "Indisponible · accès au dossier refusé"; }
            catch (IOException) { report.Message = "À vérifier · lecture des sessions interrompue"; }
            return report;
        }

        private static IEnumerable<string> ReadTail(string path)
        {
            List<string> records = new List<string>();
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                long start = Math.Max(0, stream.Length - TailBytes);
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
            string workspace = "Projet non communiqué";
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
                        workspace = Clean(name, "Projet non communiqué");
                    }
                    if (last != null) last.Workspace = workspace;
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
                            StartedUtc = eventTime, Phase = LiveTaskPhase.Active, LastWriteUtc = fileWriteUtc };
                    }
                    else if (last != null && (string.IsNullOrEmpty(turnId) || turnId == last.Id))
                    {
                        if (ev == "task_complete" || ev == "turn_complete" || ev == "turn_completed")
                        {
                            last.Phase = Json.Get(payload, "error") != null ? LiveTaskPhase.Failed : LiveTaskPhase.Completed;
                            last.FinishedUtc = eventTime == DateTimeOffset.MinValue ? (DateTimeOffset?)null : eventTime;
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
