using ColorVision.Solution.Explorer;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace ColorVision.UI.Tests;

public sealed class SolutionExtensionDiscoveryTests
{
    [Fact]
    public void MissingOptionalAttributeDependencyDoesNotHideOtherProjectProviders()
    {
        string dependencyName = "MissingSolutionAnnotation_" + Guid.NewGuid().ToString("N");
        AssemblyBuilder dependency = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(dependencyName), AssemblyBuilderAccess.RunAndCollect);
        TypeBuilder annotation = dependency.DefineDynamicModule(dependencyName)
            .DefineType("OptionalAnnotation", TypeAttributes.Public, typeof(Attribute));
        annotation.DefineDefaultConstructor(MethodAttributes.Public);
        CustomAttributeBuilder missing = new(annotation.CreateType()!.GetConstructor(Type.EmptyTypes)!, []);
        CustomAttributeBuilder projectProvider = new(typeof(ProjectProviderAttribute).GetConstructor([typeof(int)])!, [0]);

        string pluginName = "PartialSolutionPlugin_" + Guid.NewGuid().ToString("N");
        PersistedAssemblyBuilder plugin = new(new AssemblyName(pluginName), typeof(object).Assembly);
        ModuleBuilder module = plugin.DefineDynamicModule(pluginName);
        Define("UnrelatedType", typeof(object), missing);
        Define("UnavailableProvider", typeof(UnavailableProviderFixture), projectProvider, missing);
        Define("AvailableProvider", typeof(AvailableProviderFixture), projectProvider);

        using MemoryStream image = new();
        plugin.Save(image);
        Assembly loaded = Assembly.Load(image.ToArray());
        Assert.Throws<FileNotFoundException>(() => loaded.GetType("UnavailableProvider", throwOnError: true)!
            .GetCustomAttribute<ProjectProviderAttribute>());

        // The host catalog excludes in-memory assemblies. Deliver the fixture through the
        // registry's discovery callback so the test also works after another test starts the host.
        typeof(ProjectProviderRegistry).GetMethod("OnAssembliesChanged", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Action<IEnumerable<Assembly>>>()([loaded]);

        Assert.Contains("*.solution-discovery-healthy", ProjectProviderRegistry.GetProjectFilePatterns());
        Assert.DoesNotContain("*.solution-discovery-unavailable", ProjectProviderRegistry.GetProjectFilePatterns());

        void Define(string name, Type baseType, params CustomAttributeBuilder[] attributes)
        {
            TypeBuilder type = module.DefineType(name, TypeAttributes.Public, baseType);
            type.DefineDefaultConstructor(MethodAttributes.Public);
            foreach (CustomAttributeBuilder attribute in attributes)
                type.SetCustomAttribute(attribute);
            type.CreateType();
        }
    }

    public class AvailableProviderFixture : IProjectProvider, IProjectFileFormatProvider
    {
        public virtual string Id => "test.solution-discovery-healthy";
        public virtual IReadOnlyList<string> ProjectFilePatterns => ["*.solution-discovery-healthy"];
        public bool CanLoad(FileInfo projectFile) => false;
        public ProjectDefinition Load(FileInfo projectFile) => throw new NotSupportedException();
    }

    public class UnavailableProviderFixture : AvailableProviderFixture
    {
        public override string Id => "test.solution-discovery-unavailable";
        public override IReadOnlyList<string> ProjectFilePatterns => ["*.solution-discovery-unavailable"];
    }
}
