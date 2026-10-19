using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ShinoCodexMonitor
{
    internal static class Json
    {
        internal static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = 1048576 };
        internal static Dictionary<string, object> Object(object value) { return value as Dictionary<string, object>; }
        internal static object Get(Dictionary<string, object> dictionary, string key)
        {
            object value;
            return dictionary != null && dictionary.TryGetValue(key, out value) ? value : null;
        }
        internal static string String(object value) { return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture); }
        internal static int? Int(object value)
        {
            int number;
            return value != null && int.TryParse(String(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? (int?)number : null;
        }
        internal static long? Long(object value)
        {
            long number;
            return value != null && long.TryParse(String(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? (long?)number : null;
        }
        internal static double? Double(object value)
        {
            double number;
            return value != null && double.TryParse(String(value), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number) ? (double?)number : null;
        }
        internal static bool? Bool(object value) { return value is bool ? (bool?)value : null; }
    }

    internal sealed class QuotaWindow
    {
        internal int Minutes;
        internal double Remaining;
        internal DateTimeOffset? ResetUtc;
    }
    internal sealed class Snapshot
    {
        internal QuotaWindow Five;
        internal QuotaWindow Week;
        internal int? ResetsAvailable;
        internal bool? UsageAllowed;
        internal DateTimeOffset CapturedUtc;
        internal string Source = "Codex CLI / account/rateLimits/read";
    }
    internal sealed class HistoryRow
    {
        internal DateTimeOffset Utc;
        internal double? Five;
        internal double? Week;
    }
    internal sealed class Preferences
    {
        public int? X { get; set; }
        public int? Y { get; set; }
        public bool Top { get; set; }
        public double Opacity { get; set; }
        public int CircularWidth { get; set; }
        public bool SnapToEdges { get; set; }
        public string BackgroundColor { get; set; }
        public bool Alerts { get; set; }
        public bool Pinned { get; set; }
        public bool Alert50 { get; set; }
        public bool Alert25 { get; set; }
        public bool Alert10 { get; set; }
        public bool AlertResets { get; set; }
        public bool AlertFive { get; set; }
        public bool AlertWeek { get; set; }
        public bool TaskAlerts { get; set; }
        public bool TaskAnimations { get; set; }
        public string Layout { get; set; }
        public string ResetStyle { get; set; }
        public string TrayStyle { get; set; }
        public bool InTaskbar { get; set; }
        public int RefreshSeconds { get; set; }
        public bool History { get; set; }
        public string ColorHigh { get; set; }
        public string ColorMid { get; set; }
        public string ColorLow { get; set; }
        public string ColorCrit { get; set; }
        // V4.7 palette keys are independent of old 4-band custom colors.
        public string Color76 { get; set; }
        public string Color51 { get; set; }
        public string Color26 { get; set; }
        public string Color11 { get; set; }
        public string Color0 { get; set; }

        internal static Preferences Defaults()
        {
            return new Preferences {
                Top = true, Opacity = .96, CircularWidth = 540, SnapToEdges = false,
                BackgroundColor = "#111926", Alerts = true, Pinned = true,
                Alert50 = false, Alert25 = true, Alert10 = true, AlertResets = false,
                AlertFive = true, AlertWeek = true, TaskAlerts = true, TaskAnimations = true,
                Layout = "Circular", ResetStyle = "both", TrayStyle = "Percent",
                InTaskbar = false, RefreshSeconds = 60, History = true,
                ColorHigh = "#51D7AF", ColorMid = "#E5B64D", ColorLow = "#F28C38", ColorCrit = "#EE4B55",
                Color76 = "#A477FF", Color51 = "#51D7AF", Color26 = "#E5B64D",
                Color11 = "#F28C38", Color0 = "#EE4B55"
            };
        }
        internal void Sanitize()
        {
            if (Layout != "Circular" && Layout != "Vertical" && Layout != "Compact" && Layout != "Micro") Layout = "Vertical";
            if (TrayStyle != "Percent" && TrayStyle != "Ring") TrayStyle = "Percent";
            if (ResetStyle != "both" && ResetStyle != "absolute" && ResetStyle != "relative" && ResetStyle != "hidden") ResetStyle = "both";
            if (RefreshSeconds != 30 && RefreshSeconds != 60 && RefreshSeconds != 120 && RefreshSeconds != 300) RefreshSeconds = 60;
            if (Opacity < .70 || Opacity > 1.0 || double.IsNaN(Opacity)) Opacity = .96;
            if (CircularWidth < 470 || CircularWidth > 720) CircularWidth = 540;
            if (!IsDarkBackground(BackgroundColor)) BackgroundColor = "#111926";
            if (!IsHex(ColorHigh)) ColorHigh = "#51D7AF";
            if (!IsHex(ColorMid)) ColorMid = "#E5B64D";
            if (!IsHex(ColorLow)) ColorLow = "#F28C38";
            if (!IsHex(ColorCrit)) ColorCrit = "#EE4B55";
            if (!IsHex(Color76)) Color76 = "#A477FF";
            if (!IsHex(Color51)) Color51 = "#51D7AF";
            if (!IsHex(Color26)) Color26 = "#E5B64D";
            if (!IsHex(Color11)) Color11 = "#F28C38";
            if (!IsHex(Color0)) Color0 = "#EE4B55";
        }
        // Reject light backgrounds to preserve contrast of source/stale/reset text.
        internal static bool IsDarkBackground(string color)
        {
            if (!IsHex(color)) return false;
            int r = int.Parse(color.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int g = int.Parse(color.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int b = int.Parse(color.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (r * 299 + g * 587 + b * 114) / 1000 <= 70;
        }
        private static bool IsHex(string s)
        {
            if (s == null || s.Length != 7 || s[0] != '#') return false;
            for (int i = 1; i < 7; i++) if (!Uri.IsHexDigit(s[i])) return false;
            return true;
        }
    }

    internal static class QuotaHealth
    {
        internal static string State(DateTimeOffset? lastSuccess, int failures, int refreshSeconds, DateTimeOffset now)
        {
            if (!lastSuccess.HasValue) return "Unavailable";
            double age = (now - lastSuccess.Value).TotalSeconds;
            if (age < -5 || age >= refreshSeconds * 2) return "Stale data";
            if (failures > 0) return "Unverified";
            return "Direct";
        }
        internal static int Backoff(int failures, int refreshSeconds)
        {
            if (failures <= 0) return refreshSeconds;
            return (int)Math.Min(300, refreshSeconds * Math.Pow(2, Math.Min(4, failures - 1)));
        }
    }

    internal static class QuotaParser
    {
        internal static Snapshot Parse(object result)
        {
            Dictionary<string, object> root = Json.Object(result);
            if (root == null) throw new InvalidDataException("Malformed Codex response.");
            Dictionary<string, object> buckets = Json.Object(Json.Get(root, "rateLimitsByLimitId"));
            Dictionary<string, object> limits = buckets != null ? Json.Object(Json.Get(buckets, "codex")) : null;
            if (limits == null)
            {
                limits = Json.Object(Json.Get(root, "rateLimits"));
                if (limits != null)
                {
                    string limitId = Json.String(Json.Get(limits, "limitId"));
                    if (!string.IsNullOrEmpty(limitId) && limitId != "codex")
                        throw new InvalidDataException("Global Codex quota missing: model-specific quota only.");
                }
            }
            if (limits == null) throw new InvalidDataException("Global Codex quota not supplied.");
            Snapshot snap = new Snapshot();
            snap.CapturedUtc = DateTimeOffset.UtcNow;
            QuotaWindow[] all = { ReadWindow(Json.Get(limits, "primary")), ReadWindow(Json.Get(limits, "secondary")) };
            foreach (QuotaWindow w in all)
            {
                if (w == null) continue;
                if (w.Minutes == 300)
                {
                    if (snap.Five != null && (Math.Abs(snap.Five.Remaining - w.Remaining) > 0.01 || snap.Five.ResetUtc != w.ResetUtc))
                        throw new InvalidDataException("Conflicting 5-hour windows.");
                    snap.Five = w;
                }
                if (w.Minutes == 10080)
                {
                    if (snap.Week != null && (Math.Abs(snap.Week.Remaining - w.Remaining) > 0.01 || snap.Week.ResetUtc != w.ResetUtc))
                        throw new InvalidDataException("Conflicting weekly windows.");
                    snap.Week = w;
                }
            }
            if (snap.Five == null && snap.Week == null)
                throw new InvalidDataException("No valid Codex 5-hour or weekly window returned.");
            snap.UsageAllowed = Json.Bool(Json.Get(root, "ordinaryUsageAllowed"));
            Dictionary<string, object> credits = Json.Object(Json.Get(root, "rateLimitResetCredits"));
            int? available = Json.Int(Json.Get(credits, "availableCount"));
            if (available.HasValue && available.Value >= 0 && available.Value <= 10000) snap.ResetsAvailable = available;
            return snap;
        }
        private static QuotaWindow ReadWindow(object value)
        {
            Dictionary<string, object> data = Json.Object(value);
            int? duration = Json.Int(Json.Get(data, "windowDurationMins"));
            double? used = Json.Double(Json.Get(data, "usedPercent"));
            if (!duration.HasValue || (duration.Value != 300 && duration.Value != 10080) ||
                !used.HasValue || used.Value < 0 || used.Value > 100) return null;
            QuotaWindow w = new QuotaWindow { Minutes = duration.Value, Remaining = 100.0 - used.Value };
            long? seconds = Json.Long(Json.Get(data, "resetsAt"));
            if (seconds.HasValue)
            {
                try { w.ResetUtc = DateTimeOffset.FromUnixTimeSeconds(seconds.Value); }
                catch (ArgumentOutOfRangeException) { }
            }
            return w;
        }
    }
}
