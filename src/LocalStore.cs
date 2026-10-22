using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ShinoCodexMonitor
{
    internal sealed class LocalStore
    {
        internal readonly string DirectoryPath;
        private readonly string settingPath;
        private readonly string historyPath;
        private DateTimeOffset lastWrite = DateTimeOffset.MinValue;
        internal LocalStore()
        {
            DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SHINO-CODEX-MONITOR");
            System.IO.Directory.CreateDirectory(DirectoryPath);
            settingPath = Path.Combine(DirectoryPath, "settings.json");
            historyPath = Path.Combine(DirectoryPath, "history.jsonl");
            List<HistoryRow> rows = GetHistory();
            if (rows.Count != 0) lastWrite = rows[rows.Count - 1].Utc;
        }
        internal Preferences Load()
        {
            Preferences preferences = Preferences.Defaults();
            try
            {
                if (File.Exists(settingPath))
                {
                    string text = File.ReadAllText(settingPath, Encoding.UTF8).TrimStart('\uFEFF');
                    Dictionary<string, object> dict = Json.Object(Json.Serializer.DeserializeObject(text));
                    if (dict != null)
                    {
                        preferences.X = Json.Int(Json.Get(dict, "X"));
                        preferences.Y = Json.Int(Json.Get(dict, "Y"));
                        preferences.Top = Json.Bool(Json.Get(dict, "Top")) ?? preferences.Top;
                        preferences.Opacity = Json.Double(Json.Get(dict, "Opacity")) ?? preferences.Opacity;
                        preferences.CircularWidth = Json.Int(Json.Get(dict, "CircularWidth")) ?? preferences.CircularWidth;
                        preferences.ObsidianLayout=Json.Bool(Json.Get(dict,"ObsidianLayout")) ?? false; // PARITY_APPEARANCE_ONLY
                        preferences.ObsidianCircularWidth=Json.Int(Json.Get(dict,"ObsidianCircularWidth")) ?? 680; // PARITY_APPEARANCE_ONLY
                        preferences.SnapToEdges = Json.Bool(Json.Get(dict, "SnapToEdges")) ?? preferences.SnapToEdges;
                        preferences.BackgroundColor = Json.String(Json.Get(dict, "BackgroundColor")) ?? preferences.BackgroundColor;
                        preferences.Alerts = Json.Bool(Json.Get(dict, "Alerts")) ?? preferences.Alerts;
                        preferences.Pinned = Json.Bool(Json.Get(dict, "Pinned")) ?? preferences.Pinned;
                        preferences.Alert50 = Json.Bool(Json.Get(dict, "Alert50")) ?? preferences.Alert50;
                        preferences.Alert25 = Json.Bool(Json.Get(dict, "Alert25")) ?? preferences.Alert25;
                        preferences.Alert10 = Json.Bool(Json.Get(dict, "Alert10")) ?? preferences.Alert10;
                        preferences.AlertResets = Json.Bool(Json.Get(dict, "AlertResets")) ?? preferences.AlertResets;
                        preferences.AlertFive = Json.Bool(Json.Get(dict, "AlertFive")) ?? preferences.AlertFive;
                        preferences.AlertWeek = Json.Bool(Json.Get(dict, "AlertWeek")) ?? preferences.AlertWeek;
                        preferences.TaskAlerts = Json.Bool(Json.Get(dict, "TaskAlerts")) ?? preferences.TaskAlerts;
                        preferences.TaskAnimations = Json.Bool(Json.Get(dict, "TaskAnimations")) ?? preferences.TaskAnimations;
                        preferences.Layout = Json.String(Json.Get(dict, "Layout")) ?? preferences.Layout;
                        preferences.ResetStyle = Json.String(Json.Get(dict, "ResetStyle")) ?? preferences.ResetStyle;
                        preferences.TrayStyle = Json.String(Json.Get(dict, "TrayStyle")) ?? preferences.TrayStyle;
                        preferences.InTaskbar = Json.Bool(Json.Get(dict, "InTaskbar")) ?? preferences.InTaskbar;
                        preferences.RefreshSeconds = Json.Int(Json.Get(dict, "RefreshSeconds")) ?? preferences.RefreshSeconds;
                        preferences.History = Json.Bool(Json.Get(dict, "History")) ?? preferences.History;
                        preferences.ColorHigh = Json.String(Json.Get(dict, "ColorHigh")) ?? preferences.ColorHigh;
                        preferences.ColorMid = Json.String(Json.Get(dict, "ColorMid")) ?? preferences.ColorMid;
                        preferences.ColorLow = Json.String(Json.Get(dict, "ColorLow")) ?? preferences.ColorLow;
                        preferences.ColorCrit = Json.String(Json.Get(dict, "ColorCrit")) ?? preferences.ColorCrit;
                        preferences.Color76 = Json.String(Json.Get(dict, "Color76")) ?? preferences.Color76;
                        preferences.Color51 = Json.String(Json.Get(dict, "Color51")) ?? preferences.Color51;
                        preferences.Color26 = Json.String(Json.Get(dict, "Color26")) ?? preferences.Color26;
                        preferences.Color11 = Json.String(Json.Get(dict, "Color11")) ?? preferences.Color11;
                        preferences.Color0 = Json.String(Json.Get(dict, "Color0")) ?? preferences.Color0;
                    }
                }
            }
            catch { /* Invalid preferences are ignored, not interpreted as quota samples. */ }
            preferences.Sanitize();
            return preferences;
        }
        internal void Save(Preferences preferences)
        {
            preferences.Sanitize();
            try
            {
                string temp = settingPath + ".tmp";
                File.WriteAllText(temp, Json.Serializer.Serialize(preferences), new UTF8Encoding(false));
                File.Copy(temp, settingPath, true);
                File.Delete(temp);
            }
            catch { /* Optional settings persistence must not stop the live reader. */ }
        }
        internal HistoryDataset ReadHistory() { return HistoryData.Load(historyPath, DateTimeOffset.UtcNow); }
        internal List<HistoryRow> GetHistory() { return ReadHistory().Rows; }
        internal void Add(Snapshot snap, bool enabled)
        {
            if (!enabled || snap == null || (snap.Five == null && snap.Week == null)) return;
            DateTimeOffset now = snap.CapturedUtc;
            if (now - lastWrite < TimeSpan.FromMinutes(5)) return;
            HistoryRow row = new HistoryRow { Utc = now, Five = snap.Five == null ? (double?)null : snap.Five.Remaining, Week = snap.Week == null ? (double?)null : snap.Week.Remaining };
            try
            {
                Dictionary<string, object> data = new Dictionary<string, object>();
                data["utc"] = row.Utc.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
                data["five"] = row.Five;
                data["week"] = row.Week;
                data["source"] = snap.Source;
                data["fiveResetUtc"] = snap.Five == null || !snap.Five.ResetUtc.HasValue ? null : snap.Five.ResetUtc.Value.ToString("o", CultureInfo.InvariantCulture);
                data["weekResetUtc"] = snap.Week == null || !snap.Week.ResetUtc.HasValue ? null : snap.Week.ResetUtc.Value.ToString("o", CultureInfo.InvariantCulture);
                File.AppendAllText(historyPath, Json.Serializer.Serialize(data) + Environment.NewLine, new UTF8Encoding(false));
                lastWrite = now;
            }
            catch { /* History is an optional, read-only-to-Codex sidecar. */ }
        }
    }
}
