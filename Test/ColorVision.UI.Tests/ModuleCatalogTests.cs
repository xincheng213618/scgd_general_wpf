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
        public void RbacModule_RegisterMakesProviderDiscoverableFromCurrentSnapshot()
        {
            AssemblyHandler handler = AssemblyHandler.GetInstance();
            handler.ClearCaches();
            _ = handler.GetAssemblies();

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
            // The host discovers only registered assemblies, so its own first-party assemblies are listed too.
            string[] expectedBuiltInIds =
            {
                "ColorVision",
                "ColorVision.Common",
                "ColorVision.UI",
                "ColorVision.Themes",
                "ColorVision.Core",
                "ColorVision.Algorithms",
                "ColorVision.FileIO",
                "cvColorVision",
                "FlowEngineLib",
                "ST.Library.UI",
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

        [Fact]
        public void GetTypes_ReusesAssemblyTypeSnapshot()
        {
            AssemblyHandler handler = AssemblyHandler.GetInstance();
            handler.ClearCaches();
            Assembly assembly = typeof(ModuleCatalogTests).Assembly;
            handler.RegisterAssembly(assembly);

            IReadOnlyList<Type> firstSnapshot = handler.GetTypes(assembly);
            IReadOnlyList<Type> secondSnapshot = handler.GetTypes(assembly);

            Assert.NotEmpty(firstSnapshot);
            Assert.Same(firstSnapshot, secondSnapshot);

            handler.ClearCaches();
        }

        [Fact]
        public void GetTypes_PreservesLoadableExtensionsWhenAnotherTypeHasMissingDependencies()
        {
            AssemblyHandler handler = AssemblyHandler.GetInstance();
            var assembly = new PartiallyLoadableAssembly();

            IReadOnlyList<Type> types = handler.GetTypes(assembly);

            Assert.Equal(new[] { typeof(ModuleCatalogTests) }, types);
            Assert.Same(types, handler.GetTypes(assembly));
        }

        private sealed class PartiallyLoadableAssembly : Assembly
        {
            public override string FullName => "PartiallyLoadableExtensionAssembly";

            public override Type[] GetTypes() => throw new ReflectionTypeLoadException(
                new Type[] { typeof(ModuleCatalogTests), null! },
                new Exception[] { new System.IO.FileNotFoundException("Optional extension dependency is missing.") });
        }

        [Fact]
        public async Task GetImplementationTypes_InFlightQueryCannotRepopulateInvalidatedCache()
        {
            AssemblyHandler handler = AssemblyHandler.GetInstance();
            handler.ClearCaches();
            handler.RegisterAssembly(typeof(ModuleCatalogTests).Assembly);
            _ = handler.GetAssemblies();
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var serviceType = new BlockingServiceType(entered, release);
            Task<IReadOnlyList<Type>> query = Task.Run(() => handler.GetImplementationTypes(serviceType));
            try
            {
                Assert.True(entered.Wait(TimeSpan.FromSeconds(30)), "The discovery query did not enter type matching.");
                handler.ClearCaches();
                _ = handler.GetAssemblies();
            }
            finally
            {
                release.Set();
                await query;
            }

            int previousCalls = serviceType.MatchCalls;
            _ = handler.GetImplementationTypes(serviceType);
            Assert.True(serviceType.MatchCalls > previousCalls, "The invalidated query was reused instead of discovering the current snapshot.");
            handler.ClearCaches();
        }

        private sealed class BlockingServiceType(ManualResetEventSlim entered, ManualResetEventSlim release) : TypeDelegator(typeof(object))
        {
            private int _matchCalls;
            public int MatchCalls => Volatile.Read(ref _matchCalls);

            public override bool IsAssignableFrom(Type? type)
            {
                if (Interlocked.Increment(ref _matchCalls) == 1)
                {
                    entered.Set();
                    release.Wait();
                }
                return false;
            }
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
