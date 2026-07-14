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

    /// <summary>
    /// Emits the time spent registering all loaded modules' services with the DI container.
    /// </summary>
    /// <param name="elapsedMs">The measured elapsed time in milliseconds.</param>
    [Event(RegisterModulesEventId, Level = EventLevel.Informational)]
    public void RegisterModules(double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(RegisterModulesEventId, elapsedMs);
    }

    /// <summary>
    /// Emits the time spent initializing all loaded modules' services.
    /// </summary>
    /// <param name="elapsedMs">The measured elapsed time in milliseconds.</param>
    [Event(InitializeModulesEventId, Level = EventLevel.Informational)]
    public void InitializeModules(double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(InitializeModulesEventId, elapsedMs);
    }

    /// <summary>
    /// Emits the time spent loading a single module assembly from disk.
    /// </summary>
    /// <param name="moduleName">The name of the module that was loaded.</param>
    /// <param name="elapsedMs">The measured elapsed time in milliseconds.</param>
    [Event(ModuleLoadEventId, Level = EventLevel.Informational)]
    public void ModuleLoad(string moduleName, double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(ModuleLoadEventId, moduleName, elapsedMs);
    }

    /// <summary>
    /// Emits the time spent discovering and instantiating the module definitions across all loaded assemblies.
    /// </summary>
    /// <param name="elapsedMs">The measured elapsed time in milliseconds.</param>
    [Event(DiscoveryEventId, Level = EventLevel.Informational)]
    public void Discovery(double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(DiscoveryEventId, elapsedMs);
    }

    /// <summary>
    /// Emits the outcome and time spent resolving a single assembly dependency.
    /// </summary>
    /// <param name="assemblyName">The full name of the assembly being resolved.</param>
    /// <param name="resolved">Whether resolution succeeded: 1 for resolved, 0 for unresolved.</param>
    /// <param name="source">The resolution source: "cache", "local", "global", or "unresolved".</param>
    /// <param name="elapsedMs">The measured elapsed time in milliseconds.</param>
    [Event(ResolveEventId, Level = EventLevel.Informational)]
    public void Resolve(string assemblyName, int resolved, string source, double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(ResolveEventId, assemblyName, resolved, source, elapsedMs);
    }
}
