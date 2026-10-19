using System;
using System.Diagnostics;
using System.Globalization;

namespace ShinoCodexMonitor
{
    // Samples ONLY once every 15s, not once per UI repaint. No disk or telemetry upload.
    internal sealed class RuntimeMetrics
    {
        private DateTimeOffset? lastUtc;
        private TimeSpan lastCpu;
        private double? cpuPercent;
        private double ramMiB;
        private long sampleCount;

        internal void Sample(DateTimeOffset nowUtc)
        {
            if (lastUtc.HasValue && nowUtc - lastUtc.Value < TimeSpan.FromSeconds(15) && nowUtc >= lastUtc.Value)
                return;
            try
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    TimeSpan currentCpu = p.TotalProcessorTime;
                    ramMiB = p.WorkingSet64 / (1024.0 * 1024.0);
                    if (lastUtc.HasValue)
                    {
                        double elapsedMs = (nowUtc - lastUtc.Value).TotalMilliseconds;
                        double usedMs = (currentCpu - lastCpu).TotalMilliseconds;
                        if (elapsedMs > 0 && usedMs >= 0)
                            cpuPercent = Math.Min(100.0, usedMs * 100.0 / (elapsedMs * Math.Max(1, Environment.ProcessorCount)));
                    }
                    lastCpu = currentCpu;
                    lastUtc = nowUtc;
                    sampleCount++;
                }
            }
            catch { /* Measurement failure must never interrupt Codex. */ }
        }

        internal string Summary()
        {
            return "Current RAM: " + ramMiB.ToString("F1", CultureInfo.InvariantCulture) + " MiB\n" +
                "Average CPU over last interval (machine share): " +
                (cpuPercent.HasValue ? cpuPercent.Value.ToString("F2", CultureInfo.InvariantCulture) + " %" : "pending") + "\n" +
                "Performance samples: " + sampleCount;
        }
    }
}
