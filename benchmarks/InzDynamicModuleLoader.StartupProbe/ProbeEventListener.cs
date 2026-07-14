using System.Diagnostics.Tracing;

namespace InzDynamicModuleLoader.StartupProbe;

internal sealed class ProbeEventListener : EventListener
{
    public double LoadTotalMs { get; private set; }
    public double DiscoveryMs { get; private set; }
    public double ResolveTotalMs { get; private set; }
    public double ResolveMaxMs { get; private set; }
    public int ResolveCount { get; private set; }
    public int CacheHits { get; private set; }

    protected override void OnEventSourceCreated(EventSource source)
    {
        // MUST stay in sync with ModuleLoaderEventSource's [EventSource(Name = ...)] in Core.
        // Intentionally not sharing a constant to avoid touching Core's public surface;
        // the smoke test + a Group-4 runner guard cover the silent-failure risk if this drifts.
        if (source.Name == "InzSoftwares-DynamicModuleLoader")
            EnableEvents(source, EventLevel.Informational);
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        switch (e.EventId)
        {
            case 3: LoadTotalMs += Convert.ToDouble(e.Payload![1]); break;  // ModuleLoad(name, elapsedMs)
            case 4: DiscoveryMs = Convert.ToDouble(e.Payload![0]); break;   // Discovery(elapsedMs)
            case 5:                                                          // Resolve(name, resolved, source, elapsedMs)
                var ms = Convert.ToDouble(e.Payload![3]);
                ResolveTotalMs += ms;
                ResolveCount++;
                if (ms > ResolveMaxMs) ResolveMaxMs = ms;
                if ((string)e.Payload![2]! == "cache") CacheHits++;
                break;
        }
    }

    public double ResolveAvgMs => ResolveCount == 0 ? 0 : ResolveTotalMs / ResolveCount;
    public double CacheHitRatio => ResolveCount == 0 ? 0 : (double)CacheHits / ResolveCount;
}
