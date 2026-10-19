using System;
using System.Globalization;
using System.Text;

namespace ShinoCodexMonitor
{
    // A read-only, deterministic report. No CLI invocation, secrets, or network calls.
    internal static class SourceHealth
    {
        internal static string WindowLine(string label, QuotaWindow window, bool current,
            DateTimeOffset nowUtc)
        {
            if (window == null) return label + " : Window non communiquée";
            int percentage = (int)Math.Round(window.Remaining, MidpointRounding.AwayFromZero);
            percentage = Math.Max(0, Math.Min(100, percentage));
            string line = label + " : " + percentage + " % remaining";
            if (!current) return line + " (stale data · reset unverified)";
            if (!window.ResetUtc.HasValue) return line + " · Reset not provided";
            if (window.ResetUtc.Value <= nowUtc) return line + " · Reset expected · verifying...";
            return line + " · Reset " + window.ResetUtc.Value.ToLocalTime()
                .ToString("dd/MM/yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture);
        }

        internal static string Build(Snapshot snapshot, string state, DateTimeOffset? lastSuccess,
            int failures, string lastError, DateTimeOffset nextRefresh, DateTimeOffset nowUtc)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("SOURCE AND FRESHNESS");
            report.AppendLine("Origine : Codex CLI / account/rateLimits/read");
            report.AppendLine("Statut : " + (string.IsNullOrEmpty(state) ? "Unavailable" : state));
            report.AppendLine("Last valid sample: " +
                (lastSuccess.HasValue ? lastSuccess.Value.ToLocalTime()
                    .ToString("dd/MM/yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture) : "Aucun"));
            if (lastSuccess.HasValue)
            {
                double seconds = (nowUtc - lastSuccess.Value).TotalSeconds;
                report.AppendLine("Sample age: " +
                    (seconds < -5 ? "Check system clock" :
                    seconds < 60 ? "Moins d’une minute" :
                    ((int)Math.Floor(seconds / 60)).ToString(CultureInfo.InvariantCulture) + " min"));
            }
            report.AppendLine("Consecutive failures: " + failures);
            if (!string.IsNullOrEmpty(lastError))
                report.AppendLine("Last error: " + lastError);
            report.AppendLine("Prochain essai : " +
                (nextRefresh == DateTimeOffset.MinValue || nextRefresh <= nowUtc ?
                    "Imminent / en cours" : nextRefresh.ToLocalTime()
                        .ToString("dd/MM/yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture)));
            report.AppendLine();
            report.AppendLine("CODEX WINDOWS — REMAINING QUOTA");
            bool current = state == "Direct";
            if (snapshot != null && snapshot.UsageAllowed == false && current)
                report.AppendLine("⚠ Normal usage reported blocked by Codex");
            report.AppendLine(WindowLine("5 heures", snapshot == null ? null : snapshot.Five, current, nowUtc));
            report.AppendLine(WindowLine("weekly", snapshot == null ? null : snapshot.Week, current, nowUtc));
            report.AppendLine();
            report.AppendLine("Values marked stale are not current quotas.");
            report.Append("Ce rapport n’utilise ni cookies, ni jetons, ni API payante.");
            return report.ToString();
        }
    }
}
