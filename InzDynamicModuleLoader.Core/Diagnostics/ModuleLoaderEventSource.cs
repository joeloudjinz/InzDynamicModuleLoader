using System.Diagnostics.Tracing;

namespace InzDynamicModuleLoader.Core.Diagnostics;

/// <summary>
/// Emits per-phase timing for the module loading pipeline. Each event carries an already-measured
/// elapsed time (ms), so an in-process EventListener reads durations directly without pairing
/// start/stop events. Near-zero cost when no listener is attached (all guarded by IsEnabled()).
/// </summary>
[EventSource(Name = "InzSoftwares-DynamicModuleLoader")]
internal sealed class ModuleLoaderEventSource : EventSource
{
    public static readonly ModuleLoaderEventSource Log = new();

    private ModuleLoaderEventSource() { }

    public const int RegisterModulesEventId = 1;
    public const int InitializeModulesEventId = 2;
    public const int ModuleLoadEventId = 3;
    public const int DiscoveryEventId = 4;
    public const int ResolveEventId = 5;

    [Event(RegisterModulesEventId, Level = EventLevel.Informational)]
    public void RegisterModules(double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(RegisterModulesEventId, elapsedMs);
    }

    [Event(InitializeModulesEventId, Level = EventLevel.Informational)]
    public void InitializeModules(double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(InitializeModulesEventId, elapsedMs);
    }

    [Event(ModuleLoadEventId, Level = EventLevel.Informational)]
    public void ModuleLoad(string moduleName, double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(ModuleLoadEventId, moduleName, elapsedMs);
    }

    [Event(DiscoveryEventId, Level = EventLevel.Informational)]
    public void Discovery(double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(DiscoveryEventId, elapsedMs);
    }

    // resolved: 1/0, source: "cache" | "local" | "global" | "unresolved"
    [Event(ResolveEventId, Level = EventLevel.Informational)]
    public void Resolve(string assemblyName, int resolved, string source, double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(ResolveEventId, assemblyName, resolved, source, elapsedMs);
    }
}
