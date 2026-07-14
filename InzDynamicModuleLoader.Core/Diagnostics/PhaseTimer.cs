using System.Diagnostics;

namespace InzDynamicModuleLoader.Core.Diagnostics;

/// <summary>
/// Lightweight scoped timer that measures the elapsed time of a using-block and forwards it
/// (in milliseconds) to an emit callback on disposal. Keeps the simple "time this whole block"
/// phases free of repeated Stopwatch boilerplate.
/// </summary>
internal readonly struct PhaseTimer : IDisposable
{
    private readonly Action<double> _emit;
    private readonly long _startTimestamp;

    /// <summary>
    /// Starts a new timer that invokes <paramref name="emit"/> with the elapsed milliseconds when disposed.
    /// </summary>
    /// <param name="emit">The callback that receives the measured elapsed time in milliseconds.</param>
    public PhaseTimer(Action<double> emit)
    {
        _emit = emit;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Stops the timer and emits the elapsed time in milliseconds via the configured callback.
    /// </summary>
    public void Dispose()
    {
        _emit(Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds);
    }
}
