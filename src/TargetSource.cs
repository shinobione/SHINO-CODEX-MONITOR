using System;
using System.Globalization;

namespace ShinoCodexMonitor
{
    // Shared information contract: only the collector's explicit count is authority.
    internal sealed class TargetSource
    {
        internal bool Current,Blocked,Cached;
        internal string Status,CompactStatus,ReadTime,Freshness,ResetCount,Footer;
        internal static TargetSource From(Snapshot snapshot,string state,DateTimeOffset? observed,DateTimeOffset now)
        {
            TargetSource value=new TargetSource();value.Blocked=state=="Direct" && snapshot!=null && snapshot.UsageAllowed==false;
            value.Current=snapshot!=null && state=="Direct" && !value.Blocked;value.Cached=!value.Current;
            value.Status=value.Blocked?"CODEX USAGE BLOCKED":value.Current?"Direct \u00b7 Codex CLI":InstrumentStyle.IsStale(state)?"STALE READING \u00b7 CLI":state=="Unavailable"?"SOURCE UNAVAILABLE":"UNVERIFIED \u00b7 CLI";
            value.CompactStatus=value.Blocked?"BLOCKED \u00b7 CLI":value.Current?"Direct \u00b7 CLI":InstrumentStyle.IsStale(state)?"STALE \u00b7 CLI":state=="Unavailable"?"UNAVAILABLE":"UNVERIFIED \u00b7 CLI";
            value.ReadTime=observed.HasValue?"Read "+observed.Value.ToLocalTime().ToString("HH:mm:ss",CultureInfo.InvariantCulture):"No reading";
            value.Freshness=MonitorForm.FreshnessLabel(observed,now);
            int? count=snapshot==null?(int?)null:snapshot.ResetsAvailable;
            value.ResetCount=count.HasValue?count.Value.ToString(CultureInfo.InvariantCulture)+(value.Cached?" resets cached":" resets avail"):"Resets --";
            value.Footer=value.Blocked?"BLOCKED / CACHED":value.Current?"DIRECT / CLI":InstrumentStyle.IsStale(state)?"STALE / CLI":"UNVERIFIED / CLI";
            return value;
        }
    }
}
