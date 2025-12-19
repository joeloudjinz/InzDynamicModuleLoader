using InzDynamicModuleLoader.Abstractions;

namespace InzDynamicModuleLoader.Core;

internal interface IModuleManager
{
    List<IAmModule> LoadedModuleDefinitions { get; }
    
    void LoadModules(string[] moduleNames);
}