using System;

namespace ShinoCodexMonitor
{
    // Pure comparisons: no notification is made from a countdown or a single sample.
    internal static class AlertRules
    {
        internal static bool SameWindow(QuotaWindow previous, QuotaWindow latest)
        {
            return previous != null && latest != null &&
                previous.Minutes == latest.Minutes &&
                previous.ResetUtc.HasValue && latest.ResetUtc.HasValue &&
                previous.ResetUtc.Value == latest.ResetUtc.Value;
        }
        internal static int? CrossedThreshold(QuotaWindow previous, QuotaWindow latest,
            bool fifty, bool twentyFive, bool ten)
        {
            if (!SameWindow(previous, latest)) return null;
            // If multiple thresholds were crossed during one refresh, only alert the lowest.
            if (ten && previous.Remaining > 10 && latest.Remaining <= 10) return 10;
            if (twentyFive && previous.Remaining > 25 && latest.Remaining <= 25) return 25;
            if (fifty && previous.Remaining > 50 && latest.Remaining <= 50) return 50;
            return null;
        }
        internal static bool ConfirmedReset(QuotaWindow previous, QuotaWindow latest,
            DateTimeOffset capturedUtc)
        {
            return previous != null && latest != null &&
                previous.Minutes == latest.Minutes &&
                previous.ResetUtc.HasValue && latest.ResetUtc.HasValue &&
                previous.ResetUtc.Value <= capturedUtc.AddMinutes(2) &&
                latest.ResetUtc.Value > previous.ResetUtc.Value &&
                latest.Remaining >= previous.Remaining + 5.0;
        }
    }
}
