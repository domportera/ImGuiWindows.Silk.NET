using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ImGuiWindows;

internal static class StopwatchExtensions
{
    private static readonly double TicksToSeconds = 1d / Stopwatch.Frequency;

    extension(Stopwatch)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetTimestampInSeconds() => Stopwatch.GetTimestamp() * TicksToSeconds;
    }
}