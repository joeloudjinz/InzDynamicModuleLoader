using System.Reflection;
using System.Reflection.Emit;
using BenchmarkDotNet.Attributes;
using InzDynamicModuleLoader.Abstractions;
using InzDynamicModuleLoader.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InzDynamicModuleLoader.Microbench;

[MemoryDiagnoser]
public class DiscoveryBenchmarks
{
    private List<Assembly> _assemblies = null!;
    private ModuleManagerService _service = null!;

    [GlobalSetup]
    public void GlobalSetup() => _assemblies = [CreateModuleAssembly()];

    [IterationSetup]
    public void IterationSetup() => _service = new ModuleManagerService(); // discovery mutates state; fresh per op

    [Benchmark]
    public void Discover() => _service.InstantiateModuleDefinitions(_assemblies);

    private static Assembly CreateModuleAssembly()
    {
        var ab = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("MicrobenchModule"), AssemblyBuilderAccess.Run);
        var mb = ab.DefineDynamicModule("Main");
        var tb = mb.DefineType("BenchModule", TypeAttributes.Public | TypeAttributes.Class, typeof(object), [typeof(IAmModule)]);
        EmitReturnArg(tb, nameof(IAmModule.RegisterServices), typeof(IServiceCollection), [typeof(IServiceCollection), typeof(IConfiguration)]);
        EmitReturnArg(tb, nameof(IAmModule.InitializeServices), typeof(IServiceProvider), [typeof(IServiceProvider), typeof(IConfiguration)]);
        tb.CreateType();
        return ab;
    }

    private static void EmitReturnArg(TypeBuilder tb, string name, Type ret, Type[] args)
    {
        var m = tb.DefineMethod(name, MethodAttributes.Public | MethodAttributes.Virtual, ret, args);
        var il = m.GetILGenerator();
        il.Emit(OpCodes.Ldarg_1); // return the first parameter
        il.Emit(OpCodes.Ret);
        tb.DefineMethodOverride(m, typeof(IAmModule).GetMethod(name)!);
    }
}
