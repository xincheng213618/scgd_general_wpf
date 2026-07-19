using ColorVision.UI.Menus;
using System.Reflection;

namespace ColorVision.UI.Tests
{
    [CollectionDefinition(CollectionName, DisableParallelization = true)]
    public sealed class AssemblyDiscoveryCollection
    {
        public const string CollectionName = "Assembly discovery";
    }

    [Collection(AssemblyDiscoveryCollection.CollectionName)]
    public sealed class ModuleCatalogTests
    {
        [Fact]
        public void AddBuiltIn_RegistersAssemblyOnlyOnce()
        {
            var assemblyService = new RecordingAssemblyService();
            var catalog = new ModuleCatalog(assemblyService);
            Assembly assembly = typeof(ModuleCatalogTests).Assembly;

            catalog.AddBuiltIn("test.module", assembly);
            catalog.AddBuiltIn("test.module", assembly);

            ModuleRegistration registration = Assert.Single(catalog.Registrations);
            Assert.Equal("test.module", registration.Id);
            Assert.Equal(ModuleKind.BuiltIn, registration.Kind);
            Assert.Same(assembly, registration.Assembly);
            Assert.Equal(1, assemblyService.RegisterCallCount);
        }

        [Fact]
        public void Seal_RejectsAdditionalModules()
        {
            var catalog = new ModuleCatalog(new RecordingAssemblyService());
            catalog.Seal();

            Assert.Throws<InvalidOperationException>(() =>
                catalog.AddBuiltIn("test.module", typeof(ModuleCatalogTests).Assembly));
        }

        [Fact]
        public void RbacModule_RegisteredAfterSnapshot_MakesProviderDiscoverable()
        {
            AssemblyHandler handler = AssemblyHandler.GetInstance();
            handler.ClearCaches();
            Assembly[] initialSnapshot = handler.GetAssemblies();
            Assert.DoesNotContain(initialSnapshot, assembly => assembly.GetName().Name == "ColorVision.Rbac");

            Assembly rbacAssembly = Assembly.Load(new AssemblyName("ColorVision.Rbac"));
            Type? moduleType = rbacAssembly.GetType("ColorVision.Rbac.RbacModule");
            Assert.NotNull(moduleType);
            MethodInfo? registerMethod = moduleType.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(registerMethod);
            var catalog = new ModuleCatalog(handler);

            registerMethod.Invoke(null, new object[] { catalog });

            Assert.Contains(handler.GetAssemblies(), assembly => ReferenceEquals(assembly, rbacAssembly));
            Assert.Contains(
                handler.LoadImplementations<IRightMenuItemProvider>(),
                provider => provider.GetType().FullName == "ColorVision.Rbac.MenuRbacManager");

            BuiltInModules.Register(catalog);
            string[] expectedBuiltInIds =
            {
                "ColorVision.Engine",
                "ColorVision.Scheduler",
                "ColorVision.ImageEditor",
                "ColorVision.Solution",
                "ColorVision.SocketProtocol",
                "ColorVision.Database",
                "ColorVision.UI.Desktop",
                "ColorVision.ImageTools",
                "ColorVision.Rbac"
            };
            Assert.Equal(
                expectedBuiltInIds.OrderBy(id => id, StringComparer.Ordinal),
                catalog.Registrations
                    .Where(registration => registration.Kind == ModuleKind.BuiltIn)
                    .Select(registration => registration.Id)
                    .OrderBy(id => id, StringComparer.Ordinal));

            handler.ClearCaches();
        }

        private sealed class RecordingAssemblyService : IAssemblyService
        {
            public List<Assembly> Assemblies { get; } = new();
            public int RegisterCallCount { get; private set; }

            public void RegisterAssembly(Assembly assembly)
            {
                RegisterCallCount++;
                Assemblies.Add(assembly);
            }

            public Assembly[] GetAssemblies() => Assemblies.ToArray();

            public Assembly[] RefreshAssemblies() => GetAssemblies();

            public List<T> LoadImplementations<T>(params object?[]? args) where T : class => new();
        }
    }
}
