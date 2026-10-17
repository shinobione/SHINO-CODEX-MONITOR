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
            if (window == null) return label + " : Fenêtre non communiquée";
            int percentage = (int)Math.Round(window.Remaining, MidpointRounding.AwayFromZero);
            percentage = Math.Max(0, Math.Min(100, percentage));
            string line = label + " : " + percentage + " % restants";
            if (!current) return line + " (ancienne donnée · reset non vérifié)";
            if (!window.ResetUtc.HasValue) return line + " · Reset non communiqué";
            if (window.ResetUtc.Value <= nowUtc) return line + " · Reset attendu · vérification...";
            return line + " · Reset " + window.ResetUtc.Value.ToLocalTime()
                .ToString("dd/MM/yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture);
        }

        internal static string Build(Snapshot snapshot, string state, DateTimeOffset? lastSuccess,
            int failures, string lastError, DateTimeOffset nextRefresh, DateTimeOffset nowUtc)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("SOURCE ET FRAÎCHEUR");
            report.AppendLine("Origine : Codex CLI / account/rateLimits/read");
            report.AppendLine("Statut : " + (string.IsNullOrEmpty(state) ? "Indisponible" : state));
            report.AppendLine("Dernier relevé valide : " +
                (lastSuccess.HasValue ? lastSuccess.Value.ToLocalTime()
                    .ToString("dd/MM/yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture) : "Aucun"));
            if (lastSuccess.HasValue)
            {
                double seconds = (nowUtc - lastSuccess.Value).TotalSeconds;
                report.AppendLine("Âge du relevé : " +
                    (seconds < -5 ? "Horloge à vérifier" :
                    seconds < 60 ? "Moins d’une minute" :
                    ((int)Math.Floor(seconds / 60)).ToString(CultureInfo.InvariantCulture) + " min"));
            }
            report.AppendLine("Échecs consécutifs : " + failures);
            if (!string.IsNullOrEmpty(lastError))
                report.AppendLine("Dernière erreur : " + lastError);
            report.AppendLine("Prochain essai : " +
                (nextRefresh == DateTimeOffset.MinValue || nextRefresh <= nowUtc ?
                    "Imminent / en cours" : nextRefresh.ToLocalTime()
                        .ToString("dd/MM/yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture)));
            report.AppendLine();
            report.AppendLine("FENÊTRES CODEX — QUOTA RESTANT");
            bool current = state == "Direct";
            if (snapshot != null && snapshot.UsageAllowed == false && current)
                report.AppendLine("⚠ Usage ordinaire signalé bloqué par Codex");
            report.AppendLine(WindowLine("5 heures", snapshot == null ? null : snapshot.Five, current, nowUtc));
            report.AppendLine(WindowLine("7 jours", snapshot == null ? null : snapshot.Week, current, nowUtc));
            report.AppendLine();
            report.AppendLine("Les valeurs marquées anciennes ne sont pas des quotas actuels.");
            report.Append("Ce rapport n’utilise ni cookies, ni jetons, ni API payante.");
            return report.ToString();
        }
    }
}
