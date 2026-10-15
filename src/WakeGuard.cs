using System;

namespace ShinoCodexMonitor
{
    // Pure, deterministic rules for disallowing responses from before a wake/clock event.
    internal static class WakeGuard
    {
        internal static bool Gap(DateTimeOffset previousTickUtc, DateTimeOffset nowUtc)
        {
            TimeSpan elapsed = nowUtc - previousTickUtc;
            return elapsed > TimeSpan.FromSeconds(15) || elapsed < TimeSpan.FromSeconds(-5);
        }

        internal static bool Accept(int requestGeneration, int currentGeneration,
            DateTimeOffset capturedUtc, DateTimeOffset invalidAfterUtc)
        {
            return requestGeneration == currentGeneration && capturedUtc >= invalidAfterUtc;
        }
    }
}
