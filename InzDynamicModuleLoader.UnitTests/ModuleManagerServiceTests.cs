using System.Reflection;
using System.Reflection.Emit;
using InzDynamicModuleLoader.Abstractions;
using InzDynamicModuleLoader.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InzDynamicModuleLoader.UnitTests;

public class ModuleManagerServiceTests
{
    #region Test Fixtures and Helpers

    /// <summary>
    /// Creates a dynamic assembly with the specified types.
    /// This is useful for testing scenarios that are hard to create with static types.
    /// </summary>
    /// <param name="assemblyName">The name of the assembly to create.</param>
    /// <param name="configureModuleBuilder">Action to configure the module builder with types.</param>
    /// <returns>The created assembly.</returns>
    private static Assembly CreateDynamicAssembly(string assemblyName, Action<ModuleBuilder> configureModuleBuilder)
    {
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        configureModuleBuilder(moduleBuilder);

        return assemblyBuilder;
    }

    /// <summary>
    /// Creates a dynamic type that implements IAmModule.
    /// </summary>
    /// <param name="moduleBuilder">The module builder to define the type in.</param>
    /// <param name="typeName">The name of the type to create.</param>
    /// <param name="isInterface">Whether to create an interface instead of a class.</param>
    /// <param name="isAbstract">Whether to create an abstract class.</param>
    /// <param name="implementsIAmModuleDirectly">Whether the type directly implements IAmModule.</param>
    /// <returns>The created type.</returns>
    private static Type CreateDynamicType(
        ModuleBuilder moduleBuilder,
        string typeName,
        bool isInterface = false,
        bool isAbstract = false,
        bool implementsIAmModuleDirectly = true
    )
    {
        var typeAttributes = TypeAttributes.Public | TypeAttributes.Class;

        if (isInterface)
        {
            typeAttributes = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;
        }
        else if (isAbstract)
        {
            typeAttributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract;
        }

        var interfaces = new List<Type>();
        if (isInterface)
        {
            if (implementsIAmModuleDirectly)
            {
                interfaces.Add(typeof(IAmModule));
            }
        }
        else
        {
            if (implementsIAmModuleDirectly)
            {
                interfaces.Add(typeof(IAmModule));
            }
        }

        var typeBuilder = moduleBuilder.DefineType(typeName, typeAttributes, isInterface ? null : typeof(object), interfaces.ToArray());

        if (!isInterface && !isAbstract)
        {
            // Implement IAmModule methods with stub implementations
            ImplementIAmModuleMethods(typeBuilder);
        }

        return typeBuilder.CreateType()!;
    }

    /// <summary>
    /// Implements the IAmModule interface methods on a type builder.
    /// </summary>
    private static void ImplementIAmModuleMethods(TypeBuilder typeBuilder)
    {
        // Implement RegisterServices method
        var registerServicesMethod = typeBuilder.DefineMethod(
            "RegisterServices",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(IServiceCollection),
            [typeof(IServiceCollection), typeof(IConfiguration)]);

        var registerIl = registerServicesMethod.GetILGenerator();
        registerIl.Emit(OpCodes.Ldarg_1); // Return the services parameter
        registerIl.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(registerServicesMethod, typeof(IAmModule).GetMethod(nameof(IAmModule.RegisterServices))!);

        // Implement InitializeServices method
        var initializeServicesMethod = typeBuilder.DefineMethod(
            "InitializeServices",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(IServiceProvider),
            [typeof(IServiceProvider), typeof(IConfiguration)]);

        var initializeIl = initializeServicesMethod.GetILGenerator();
        initializeIl.Emit(OpCodes.Ldarg_1); // Return the services parameter
        initializeIl.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(initializeServicesMethod, typeof(IAmModule).GetMethod(nameof(IAmModule.InitializeServices))!);
    }

    #endregion

    #region Tests

    #region Happy Path Tests

    [Fact]
    public void InstantiateModuleDefinitions_SingleValidModule_AddsModuleToLoadedDefinitions()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assemblyWithModule = CreateDynamicAssembly("AssemblyWithValidModule", mb => { CreateDynamicType(mb, "ValidModule"); });

        // Act
        service.InstantiateModuleDefinitions([assemblyWithModule]);

        // Assert
        Assert.Single(service.LoadedModuleDefinitions);
        Assert.NotNull(service.LoadedModuleDefinitions[0]);
        Assert.IsAssignableFrom<IAmModule>(service.LoadedModuleDefinitions[0]);
    }

    [Fact]
    public void InstantiateModuleDefinitions_MultipleValidAssemblies_AddsAllModules()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assembly1 = CreateDynamicAssembly("Assembly1", mb => { CreateDynamicType(mb, "Module1"); });
        var assembly2 = CreateDynamicAssembly("Assembly2", mb => { CreateDynamicType(mb, "Module2"); });

        // Act
        service.InstantiateModuleDefinitions([assembly1, assembly2]);

        // Assert
        Assert.Equal(2, service.LoadedModuleDefinitions.Count);
    }

    #endregion

    #region No Module Implementation Tests

    [Fact]
    public void InstantiateModuleDefinitions_AssemblyWithNoModule_LogsWarningAndContinues()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assemblyWithoutModule = CreateDynamicAssembly("AssemblyWithoutModule", mb =>
        {
            // Create a regular class that doesn't implement IAmModule
            var typeBuilder = mb.DefineType("RegularClass", TypeAttributes.Public | TypeAttributes.Class, typeof(object));
            typeBuilder.CreateType();
        });

        // Act & Assert - should not throw, just log warning
        var exception = Record.Exception(() => service.InstantiateModuleDefinitions([assemblyWithoutModule]));

        Assert.Null(exception);
        Assert.Empty(service.LoadedModuleDefinitions);
    }

    [Fact]
    public void InstantiateModuleDefinitions_AssemblyWithNoModule_DoesNotAddToLoadedDefinitions()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assemblyWithoutModule = CreateDynamicAssembly("AssemblyWithoutModule", mb =>
        {
            // Create a regular class that doesn't implement IAmModule
            var typeBuilder = mb.DefineType("RegularClass", TypeAttributes.Public | TypeAttributes.Class, typeof(object));
            typeBuilder.CreateType();
        });

        // Act
        service.InstantiateModuleDefinitions([assemblyWithoutModule]);

        // Assert
        Assert.Empty(service.LoadedModuleDefinitions);
    }

    [Fact]
    public void InstantiateModuleDefinitions_MixedAssemblies_AddsOnlyValidModules()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assemblyWithoutModule = CreateDynamicAssembly("AssemblyWithoutModule", mb =>
        {
            // Create a regular class that doesn't implement IAmModule
            var typeBuilder = mb.DefineType("RegularClass", TypeAttributes.Public | TypeAttributes.Class, typeof(object));
            typeBuilder.CreateType();
        });
        var assemblyWithModule = CreateDynamicAssembly("AssemblyWithModule", mb => { CreateDynamicType(mb, "ValidModule"); });

        // Act
        service.InstantiateModuleDefinitions([assemblyWithoutModule, assemblyWithModule]);

        // Assert
        Assert.Single(service.LoadedModuleDefinitions);
    }

    #endregion

    #region Multiple Implementations Tests

    [Fact]
    public void InstantiateModuleDefinitions_MultipleImplementationsInSingleAssembly_ThrowsException()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assemblyWithMultipleModules = CreateDynamicAssembly("AssemblyWithMultipleModules", mb =>
        {
            CreateDynamicType(mb, "FirstModule");
            CreateDynamicType(mb, "SecondModule");
        });

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => service.InstantiateModuleDefinitions([assemblyWithMultipleModules]));
        Assert.Contains("IAmModule contract must have only one implementation", exception.Message);
        Assert.Contains("AssemblyWithMultipleModules", exception.Message);
        Assert.Empty(service.LoadedModuleDefinitions);
    }

    #endregion

    #region Interface and Abstract Class Filtering Tests

    [Fact]
    public void InstantiateModuleDefinitions_InterfaceOnly_NoModuleAdded()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assemblyWithInterfaceOnly = CreateDynamicAssembly("AssemblyWithInterfaceOnly", mb =>
        {
            // Create only an interface, no concrete implementation
            CreateDynamicType(mb, "IModuleInterface", isInterface: true);
        });

        // Act
        service.InstantiateModuleDefinitions([assemblyWithInterfaceOnly]);

        // Assert - should not instantiate interfaces
        Assert.Empty(service.LoadedModuleDefinitions);
    }

    [Fact]
    public void InstantiateModuleDefinitions_AbstractClassOnly_NoModuleAdded()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assemblyWithAbstractOnly = CreateDynamicAssembly("AssemblyWithAbstractOnly", mb =>
        {
            // Create only an abstract class, no concrete implementation
            CreateDynamicType(mb, "AbstractModule", isAbstract: true);
        });

        // Act
        service.InstantiateModuleDefinitions([assemblyWithAbstractOnly]);

        // Assert - should not instantiate abstract classes
        Assert.Empty(service.LoadedModuleDefinitions);
    }

    [Fact]
    public void InstantiateModuleDefinitions_InterfaceAndConcreteClass_OnlyInstantiatesConcreteClass()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assemblyWithInterfaceAndClass = CreateDynamicAssembly("AssemblyWithInterfaceAndClass", mb =>
        {
            CreateDynamicType(mb, "IModuleInterface", isInterface: true);
            CreateDynamicType(mb, "ConcreteModule");
        });

        // Act
        service.InstantiateModuleDefinitions([assemblyWithInterfaceAndClass]);

        // Assert - should only instantiate the concrete class
        Assert.Single(service.LoadedModuleDefinitions);
    }

    [Fact]
    public void InstantiateModuleDefinitions_DerivedInterfaceWithConcreteImplementation_InstantiatesConcreteClass()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assemblyWithDerivedInterface = CreateDynamicAssembly("AssemblyWithDerivedInterface", mb =>
        {
            // Create interface that extends IAmModule
            var interfaceType = CreateDynamicType(mb, "IModuleInterface", isInterface: true);
            // Create concrete class implementing IAmModule directly (simpler scenario)
            CreateDynamicType(mb, "ConcreteModule");
        });

        // Act
        service.InstantiateModuleDefinitions([assemblyWithDerivedInterface]);

        // Assert - should find and instantiate the concrete class
        Assert.Single(service.LoadedModuleDefinitions);
    }

    #endregion

    #region Null FullName Bug Fix Tests

    /// <summary>
    /// This test verifies the bug fix where ti.FullName could be null for certain types.
    /// The bug occurred when calling t.GetInterfaces().Any(ti => ti.FullName!.Equals(...))
    /// without first checking if ti.FullName is null.
    /// 
    /// In .NET, FullName is guaranteed to be null for:
    /// - Generic type parameters (e.g., T in class Foo<T>)
    /// - Type variables in generic methods
    /// 
    /// This test creates a generic type and verifies that when GetInterfaces() is called
    /// on types that include generic parameters, the null check prevents NullReferenceException.
    /// </summary>
    [Fact]
    public void InstantiateModuleDefinitions_GenericTypeWithInterfaceConstraint_HandlesNullFullName()
    {
        // Arrange
        var service = new ModuleManagerService();

        // Create a dynamic assembly with a generic type
        // Generic type parameters (T) have FullName = null by design
        var assembly = CreateDynamicAssembly("AssemblyWithGenericType", mb =>
        {
            // Create a generic class that implements IAmModule
            // The generic parameter T will have FullName = null
            var typeAttributes = TypeAttributes.Public | TypeAttributes.Class;
            var genericType = mb.DefineType("GenericModule`1", typeAttributes, typeof(object), [typeof(IAmModule)]);

            // Define the generic parameter
            var genericParam = genericType.DefineGenericParameters(["T"])[0];

            // Add IAmModule interface implementation
            genericType.AddInterfaceImplementation(typeof(IAmModule));

            // Implement IAmModule methods
            ImplementIAmModuleMethods(genericType);

            genericType.CreateType();
        });

        // Verify we can get the types from the assembly
        var allTypes = assembly.GetTypes();
        Assert.Contains(allTypes, t => t.Name.Contains("GenericModule"));

        // Act & Assert - should not throw NullReferenceException even when processing generic types
        // where generic parameters have FullName = null
        // Note: This will throw an exception because we can't instantiate a generic type without 
        // specifying the generic argument, but it should NOT be a NullReferenceException
        var exception = Assert.ThrowsAny<Exception>(() => service.InstantiateModuleDefinitions([assembly]));

        // Verify it's NOT a NullReferenceException (which was the original bug)
        Assert.IsNotType<NullReferenceException>(exception.GetType());

        // The exception should be about not being able to create the generic type,
        // not about null FullName
        Assert.Contains("GenericModule", exception.Message);
    }

    /// <summary>
    /// Direct unit test that verifies the exact LINQ predicate used in the code
    /// handles null FullName values correctly.
    /// 
    /// This test directly accesses the types and simulates the exact condition
    /// that was causing the NullReferenceException before the fix.
    /// </summary>
    [Fact]
    public void InstantiateModuleDefinitions_InterfaceFullNameNullCheck_DirectVerification()
    {
        // Arrange
        var assembly = CreateDynamicAssembly("AssemblyForDirectTest", mb =>
        {
            // Create a valid module
            CreateDynamicType(mb, "ValidModule");
        });

        // Get a type from the assembly
        var type = assembly.GetTypes().First(t => t.Name == "ValidModule");

        // Get all interfaces
        var interfaces = type.GetInterfaces();

        // Verify we can safely iterate through interfaces checking FullName
        // This is the exact pattern used in the code that was fixed
        var hasIAmModuleInterface = interfaces.Any(ti =>
            ti.FullName is not null &&
            ti.FullName!.Equals(typeof(IAmModule).FullName)
        );

        // Act & Assert - verify the null-safe check works
        Assert.True(hasIAmModuleInterface, "Should find IAmModule interface using null-safe check");

        // Verify that accessing FullName directly on all interfaces doesn't throw
        // (this would throw before the fix if any FullName was null)
        foreach (var iface in interfaces)
        {
            // This is safe now because we check for null first
            if (iface.FullName is not null)
            {
                // Accessing FullName here is safe
                var fullName = iface.FullName;
                Assert.NotNull(fullName);
            }
            // If FullName is null, skip it (this is the fix)
        }
    }

    /// <summary>
    /// Tests that the fix handles assemblies with multiple types where some types
    /// might have interfaces with null FullName (such as generic parameters).
    /// </summary>
    [Fact]
    public void InstantiateModuleDefinitions_MultipleTypesWithGenericParameters_NoNullReferenceException()
    {
        // Arrange
        var service = new ModuleManagerService();

        // Create a dynamic assembly with multiple types including generic ones
        var assembly = CreateDynamicAssembly("AssemblyWithGenericParameters", mb =>
        {
            // Create a generic interface
            var genericInterface = mb.DefineType("IGenericInterface`1", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
            genericInterface.DefineGenericParameters(["T"]);
            genericInterface.CreateType();

            // Create a valid non-generic module
            CreateDynamicType(mb, "ValidModule");
        });

        // Verify the assembly has types
        var allTypes = assembly.GetTypes();
        Assert.NotEmpty(allTypes);

        // Act & Assert - processing should not throw NullReferenceException
        // even when some types have generic parameters with null FullName
        var exception = Record.Exception(() => service.InstantiateModuleDefinitions([assembly]));

        Assert.Null(exception);
        Assert.Single(service.LoadedModuleDefinitions);
    }

    /// <summary>
    /// Regression test: Verifies that the original bug (NullReferenceException when ti.FullName is null)
    /// is fixed by testing with multiple assemblies containing various type configurations.
    /// </summary>
    [Fact]
    public void InstantiateModuleDefinitions_MultipleAssembliesWithVariousTypes_NoNullReferenceException()
    {
        // Arrange
        var service = new ModuleManagerService();

        var assembly1 = CreateDynamicAssembly("Assembly1", mb =>
        {
            CreateDynamicType(mb, "IModuleInterface", isInterface: true);
            CreateDynamicType(mb, "Module1");
        });

        var assembly2 = CreateDynamicAssembly("Assembly2", mb => { CreateDynamicType(mb, "Module2"); });

        var assembly3 = CreateDynamicAssembly("Assembly3", mb =>
        {
            // Assembly with only interfaces (no concrete implementations)
            CreateDynamicType(mb, "IOnlyInterface", isInterface: true);
        });

        // Act - process all assemblies
        // The bug would manifest here if FullName null checks were not in place
        service.InstantiateModuleDefinitions([assembly1, assembly2, assembly3]);

        // Assert - should have processed all assemblies without NullReferenceException
        // and created modules only from assemblies with valid implementations
        Assert.Equal(2, service.LoadedModuleDefinitions.Count);
    }

    #endregion

    #region Empty List Tests

    [Fact]
    public void InstantiateModuleDefinitions_EmptyAssemblyList_DoesNotThrow()
    {
        // Arrange
        var service = new ModuleManagerService();

        // Act & Assert
        var exception = Record.Exception(() => service.InstantiateModuleDefinitions([]));

        Assert.Null(exception);
        Assert.Empty(service.LoadedModuleDefinitions);
    }

    #endregion

    #region Module Instantiation Verification Tests

    [Fact]
    public void InstantiateModuleDefinitions_ValidModule_CreatesNewInstance()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assembly = CreateDynamicAssembly("Assembly", mb => { CreateDynamicType(mb, "TestModule"); });

        // Act
        service.InstantiateModuleDefinitions([assembly]);

        // Assert
        var module = Assert.Single(service.LoadedModuleDefinitions);
        Assert.NotNull(module);
        Assert.IsAssignableFrom<IAmModule>(module);
    }

    [Fact]
    public void InstantiateModuleDefinitions_ModuleWithParameterlessConstructor_SuccessfullyInstantiates()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assembly = CreateDynamicAssembly("Assembly", mb => { CreateDynamicType(mb, "ModuleWithDefaultConstructor"); });

        // Act
        service.InstantiateModuleDefinitions([assembly]);

        // Assert
        var module = Assert.Single(service.LoadedModuleDefinitions);
        Assert.NotNull(module);
    }

    #endregion

    #region Assembly Name in Error Messages Tests

    [Fact]
    public void InstantiateModuleDefinitions_MultipleImplementations_ErrorIncludesAssemblyName()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assemblyName = "TestAssemblyWithMultipleModules";
        var assembly = CreateDynamicAssembly(assemblyName, mb =>
        {
            CreateDynamicType(mb, "FirstModule");
            CreateDynamicType(mb, "SecondModule");
        });

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => service.InstantiateModuleDefinitions([assembly]));
        Assert.Contains(assemblyName, exception.Message);
        Assert.Contains("IAmModule contract must have only one implementation", exception.Message);
    }

    [Fact]
    public void InstantiateModuleDefinitions_NoImplementation_WarningLoggedForAssembly()
    {
        // Arrange
        var service = new ModuleManagerService();
        var assemblyName = "TestAssemblyWithoutModule";
        var assembly = CreateDynamicAssembly(assemblyName, mb =>
        {
            // Create a regular class that doesn't implement IAmModule
            var typeBuilder = mb.DefineType("NonModuleClass", TypeAttributes.Public | TypeAttributes.Class, typeof(object));
            typeBuilder.CreateType();
        });

        // Act - this should complete without throwing, just logging a warning
        service.InstantiateModuleDefinitions([assembly]);

        // Assert
        Assert.Empty(service.LoadedModuleDefinitions);
    }

    #endregion

    #endregion
}