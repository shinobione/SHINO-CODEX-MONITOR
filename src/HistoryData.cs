using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ShinoCodexMonitor
{
    internal enum HistorySeries { Five, Weekly, Both }
    internal sealed class HistoryDataset
    {
        internal readonly List<HistoryRow> Rows = new List<HistoryRow>();
        internal int InvalidRows;
        internal bool Unavailable;
        internal bool UnlocatedGaps;
    }
    internal sealed class HistoryPoint
    {
        internal DateTimeOffset Utc;
        internal double Remaining;
        internal bool Five, ConnectPrevious, ObservedReset;
        internal DateTimeOffset? ExpectedReset;
        internal string Source;
        internal string Tooltip
        {
            get { return Utc.ToLocalTime().ToString("dd MMM yyyy · HH:mm:ss zzz", HistoryData.English) +
                "\n" + (Five ? "5H" : "Weekly") + " Remaining: " + Remaining.ToString("0.##", HistoryData.English) + "%" +
                "\nSource: " + Source + "\nSample status: Valid recorded observation" +
                (ExpectedReset.HasValue ? "\nExpected reset: " + ExpectedReset.Value.ToLocalTime().ToString("dd MMM · HH:mm zzz", HistoryData.English) : "") +
                (ObservedReset ? "\nObserved reset: replenishment + advanced reset window" : ""); }
        }
    }
    internal sealed class HistoryView
    {
        internal readonly List<HistoryPoint> Points = new List<HistoryPoint>();
        internal DateTimeOffset Start, End;
        internal string Message, Summary;
        internal int ValidRows, Gaps;
    }
    internal static class HistoryData
    {
        internal static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");
        internal static readonly TimeSpan ContinuityGap = TimeSpan.FromMinutes(30);
        // No retention rewrite: ALL means all retained source rows, including V5.1.
        internal static HistoryDataset Load(string path, DateTimeOffset now)
        {
            try {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    return Normalize(Lines(reader), now);
            }
            catch (FileNotFoundException) { return new HistoryDataset(); }
            catch (DirectoryNotFoundException) { return new HistoryDataset(); }
            catch (IOException) { return new HistoryDataset { Unavailable = true }; }
            catch (UnauthorizedAccessException) { return new HistoryDataset { Unavailable = true }; }
        }
        private static IEnumerable<string> Lines(StreamReader reader)
        { string line; while ((line = reader.ReadLine()) != null) yield return line; }
        private static DateTimeOffset? Instant(object value)
        {
            string s = Json.String(value);
            // A local wall-clock time without an offset cannot locate an observation.
            if (string.IsNullOrEmpty(s) || !(s.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(s, @"[+-]\d{2}:\d{2}$"))) return null;
            DateTimeOffset t;
            return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t) ? (DateTimeOffset?)t.ToUniversalTime() : null;
        }
        private static double? Percent(object value)
        { double? p = Json.Double(value); return p.HasValue && p >= 0 && p <= 100 ? p : null; }
        internal static HistoryDataset Normalize(IEnumerable<string> lines, DateTimeOffset now)
        {
            HistoryDataset result = new HistoryDataset();
            Dictionary<DateTimeOffset, HistoryRow> byTime = new Dictionary<DateTimeOffset, HistoryRow>();
            foreach (string line in lines) {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try {
                    Dictionary<string, object> d = Json.Object(Json.Serializer.DeserializeObject(line.TrimStart('\uFEFF')));
                    DateTimeOffset? t = Instant(Json.Get(d, "utc"));
                    if (!t.HasValue || t.Value > now.AddMinutes(2)) { result.InvalidRows++; result.UnlocatedGaps = true; continue; }
                    string status = Json.String(Json.Get(d, "status"));
                    bool valid = string.IsNullOrEmpty(status) || status.Equals("valid", StringComparison.OrdinalIgnoreCase);
                    double? five = valid ? Percent(Json.Get(d, "five")) : null;
                    double? week = valid ? Percent(Json.Get(d, "week")) : null;
                    bool bad = !valid || (Json.Get(d, "five") != null && !five.HasValue) ||
                        (Json.Get(d, "week") != null && !week.HasValue) || (!five.HasValue && !week.HasValue);
                    if (bad) result.InvalidRows++;
                    HistoryRow row = new HistoryRow { Utc = t.Value, Five = five, Week = week,
                        FiveResetUtc = Instant(Json.Get(d, "fiveResetUtc")), WeekResetUtc = Instant(Json.Get(d, "weekResetUtc")),
                        Source = SafeSource(Json.String(Json.Get(d, "source"))),
                        FiveConflict = Json.Get(d, "five") != null && !five.HasValue,
                        WeekConflict = Json.Get(d, "week") != null && !week.HasValue };
                    HistoryRow old;
                    if (byTime.TryGetValue(row.Utc, out old)) {
                        // Conflicting measurements at the same instant cannot be averaged or chosen.
                        bool fc = old.FiveConflict || row.FiveConflict || (old.Five.HasValue && row.Five.HasValue && old.Five != row.Five) ||
                            (old.FiveResetUtc.HasValue && row.FiveResetUtc.HasValue && old.FiveResetUtc != row.FiveResetUtc);
                        bool wc = old.WeekConflict || row.WeekConflict || (old.Week.HasValue && row.Week.HasValue && old.Week != row.Week) ||
                            (old.WeekResetUtc.HasValue && row.WeekResetUtc.HasValue && old.WeekResetUtc != row.WeekResetUtc);
                        if (fc || wc) result.InvalidRows++;
                        old.Five = fc ? null : old.Five ?? row.Five; old.Week = wc ? null : old.Week ?? row.Week;
                        old.FiveResetUtc = old.FiveResetUtc ?? row.FiveResetUtc; old.WeekResetUtc = old.WeekResetUtc ?? row.WeekResetUtc;
                        old.FiveConflict = fc; old.WeekConflict = wc;
                    } else byTime.Add(row.Utc, row);
                }
                catch (ArgumentException) { result.InvalidRows++; result.UnlocatedGaps = true; }
                catch (InvalidOperationException) { result.InvalidRows++; result.UnlocatedGaps = true; }
            }
            result.Rows.AddRange(byTime.Values.OrderBy(r => r.Utc));
            return result;
        }
        private static string SafeSource(string value)
        {
            // Historical versions stored no source; do not invent provenance.
            if (value == "Codex CLI / account/rateLimits/read" || value == "Codex CLI") return value;
            return "Not recorded (legacy/local history)";
        }
        internal static HistoryView Select(HistoryDataset data, DateTimeOffset now, int hours, HistorySeries series)
        {
            HistoryView v = new HistoryView { End = now, Start = now, Summary = "LATEST —    LOWEST —    HIGHEST —    SAMPLES 0" };
            if (data.Unavailable) { v.Message = "History unavailable"; return v; }
            if (data.Rows.Count == 0) { v.Message = data.InvalidRows > 0 ? "Some samples could not be validated" : "No history recorded yet"; return v; }
            DateTimeOffset cutoff = hours == 0 ? data.Rows[0].Utc : now.AddHours(-hours);
            List<HistoryRow> rows = data.Rows.Where(r => r.Utc >= cutoff && r.Utc <= now).ToList();
            // Do not extend the axis before the first retained observation.
            v.Start = cutoff > data.Rows[0].Utc ? cutoff : data.Rows[0].Utc;
            foreach (HistoryRow r in rows) if ((series != HistorySeries.Weekly && r.Five.HasValue) || (series != HistorySeries.Five && r.Week.HasValue)) v.ValidRows++;
            if (series != HistorySeries.Weekly) AddSeries(v, rows, true);
            if (series != HistorySeries.Five) AddSeries(v, rows, false);
            if (data.UnlocatedGaps) {
                foreach (HistoryPoint point in v.Points) point.ConnectPrevious = false;
                v.Gaps++;
            }
            v.Points.Sort(delegate(HistoryPoint a, HistoryPoint b) { int c = a.Utc.CompareTo(b.Utc); return c == 0 ? b.Five.CompareTo(a.Five) : c; });
            if (v.Points.Count == 0) { v.Message = "No samples in selected range"; return v; }
            List<string> summaries = new List<string>();
            foreach (bool five in new [] { true, false }) {
                List<HistoryPoint> p = v.Points.Where(x => x.Five == five).ToList();
                if (p.Count == 0) continue;
                summaries.Add((five ? "5H" : "WEEKLY") + "   LATEST " + Pct(p[p.Count - 1].Remaining) + "   LOWEST " + Pct(p.Min(x => x.Remaining)) +
                    "   HIGHEST " + Pct(p.Max(x => x.Remaining)) + "   SAMPLES " + p.Count);
            }
            v.Summary = string.Join("\n", summaries.ToArray());
            v.Message = v.Gaps > 0 ? "Gaps and window changes are not interpolated" : "Recorded observations · straight segments only";
            return v;
        }
        internal static string Pct(double value) { return value.ToString("0.##", English) + "%"; }
        private static void AddSeries(HistoryView view, List<HistoryRow> rows, bool five)
        {
            HistoryPoint previous = null;
            foreach (HistoryRow row in rows) {
                double? value = five ? row.Five : row.Week;
                if (!value.HasValue) { previous = null; continue; }
                DateTimeOffset? reset = five ? row.FiveResetUtc : row.WeekResetUtc;
                HistoryPoint point = new HistoryPoint { Utc = row.Utc, Remaining = value.Value, Five = five, ExpectedReset = reset, Source = row.Source };
                if (previous != null) {
                    bool continuous = row.Utc - previous.Utc <= ContinuityGap;
                    bool rise = point.Remaining > previous.Remaining;
                    bool resetChange = previous.ExpectedReset.HasValue && reset.HasValue && previous.ExpectedReset != reset;
                    point.ObservedReset = continuous && rise && resetChange && reset.Value > previous.ExpectedReset.Value &&
                        previous.ExpectedReset.Value > previous.Utc && previous.ExpectedReset.Value <= point.Utc && reset.Value > point.Utc;
                    point.ConnectPrevious = continuous && !rise && !resetChange &&
                        previous.ExpectedReset.HasValue == reset.HasValue && previous.Source == point.Source;
                    if (!point.ConnectPrevious) view.Gaps++;
                }
                view.Points.Add(point); previous = point;
            }
        }
    }
}
