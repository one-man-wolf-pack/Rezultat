using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class CostMeasurement
{
    private const int Iterations = 20_000;
    private const int WarmupRounds = 50;

    public static (double Bytes, double Nanoseconds) Measure(Action<int> kernel)
    {
        for (var round = 0; round < WarmupRounds; round++)
        {
            kernel(Iterations / 10);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        kernel(Iterations);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var bytes = (double)(GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
        return (bytes, elapsed.TotalNanoseconds / Iterations);
    }

    public static Large CreateLarge(long first)
    {
        var large = default(Large);
        large[0] = first;
        return large;
    }
}

[InlineArray(64)]
internal struct Large
{
    private long _element;
}
