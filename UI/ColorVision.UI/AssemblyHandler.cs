#pragma warning disable CS8602
using log4net;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI
{
    /// <summary>
    /// Extension methods for Application to access assembly services
    /// </summary>
    public static class AssemblyHandlerExtensions
    {
        /// <summary>
        /// Gets all filtered custom assemblies from the application domain
        /// </summary>
        public static Assembly[] GetAssemblies(this Application application)
        {
            return AssemblyHandler.Instance.GetAssemblies();
        }
    }

    /// <summary>
    /// Manages assembly loading, filtering, and type discovery with caching for performance
    /// </summary>
    public class AssemblyHandler: IAssemblyService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AssemblyHandler));
        private static readonly Lazy<AssemblyHandler> LazyInstance = new(() => new AssemblyHandler());
        /// <summary>
        /// Gets the singleton instance of AssemblyHandler
        /// </summary>
        public static AssemblyHandler Instance => LazyInstance.Value;

        public static AssemblyHandler GetInstance() => Instance;

        // Prefixes for system assemblies to exclude from discovery
        private static readonly HashSet<string> ExcludedAssemblyPrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "System.", "Microsoft.", "netstandard", "WindowsBase",
            "PresentationCore", "PresentationFramework", "mscorlib",
            "Newtonsoft", "EntityFramework", "log4net", "HandyControl"
        };

        private Assembly[]? _assemblies;
        private readonly HashSet<Assembly> _registeredAssemblies = new();
        private readonly ConcurrentDictionary<Assembly, Type[]> _assemblyTypeCache = new();
        private ConcurrentDictionary<Type, Type[]> _implementationTypeCache = new();
        private readonly object _assembliesLock = new();
        private bool _registeredAssembliesOnly;

        public event EventHandler? AssembliesChanged;

        /// <summary>
        /// Restricts discovery to explicitly registered assemblies, i.e. the host's module catalog.
        /// Without this switch every loaded assembly under the application directory is scanned,
        /// which keeps tests and standalone hosts working without a catalog.
        /// </summary>
        public void UseRegisteredAssembliesOnly()
        {
            bool hasSnapshot;
            lock (_assembliesLock)
            {
                if (_registeredAssembliesOnly)
                    return;
                _registeredAssembliesOnly = true;
                hasSnapshot = _assemblies != null;
            }

            AppDomain.CurrentDomain.AssemblyLoad += (_, e) => WarnIfUnregisteredContributor(e.LoadedAssembly);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                WarnIfUnregisteredContributor(assembly);

            if (hasSnapshot)
                RefreshAssemblies();
        }

        /// <summary>
        /// Catalog-only discovery silently skips a first-party assembly that was never registered,
        /// so report application-directory assemblies that reference the extension contracts.
        /// Plugin assemblies are excluded because PluginLoader registers them right after loading.
        /// </summary>
        private void WarnIfUnregisteredContributor(Assembly assembly)
        {
            try
            {
                lock (_assembliesLock)
                {
                    if (_registeredAssemblies.Contains(assembly))
                        return;
                }

                string? name = assembly.GetName().Name;
                if (!IsCustomAssembly(name, assembly) || !TryGetAssemblyLocation(assembly, out string? location))
                    return;

                string pluginDirectory = EnsureTrailingDirectorySeparator(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"));
                if (location!.StartsWith(pluginDirectory, StringComparison.OrdinalIgnoreCase))
                    return;

                if (assembly.GetReferencedAssemblies().Any(reference => reference.Name is "ColorVision.Common" or "ColorVision.UI"))
                    Log.Warn($"Assembly '{name}' references ColorVision extension contracts but is not registered in the module catalog; its extensions are not discovered.");
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not inspect assembly '{assembly.FullName}' for catalog registration.", ex);
            }
        }

        private AssemblyHandler()
        {
            AssemblyService.SetInstance(this);
        }

        /// <summary>
        /// Registers an assembly that intentionally contributes application features.
        /// Registration also updates an existing discovery snapshot so correctness no
        /// longer depends on whether the assembly was loaded before the first scan.
        /// </summary>
        public void RegisterAssembly(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            if (assembly.IsDynamic)
                throw new ArgumentException("Dynamic assemblies cannot be registered as application modules.", nameof(assembly));

            bool changed = false;
            lock (_assembliesLock)
            {
                if (!_registeredAssemblies.Add(assembly))
                    return;

                if (_assemblies != null
                    && !_assemblies.Contains(assembly)
                    && IsCustomAssembly(assembly.GetName().Name, assembly)
                    && TryLoadAssemblyTypes(assembly, out _))
                {
                    _assemblies = _assemblies.Append(assembly).ToArray();
                    changed = true;
                }

                Volatile.Write(ref _implementationTypeCache, new());
            }

            if (changed)
                AssembliesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Determines if an assembly is a custom (non-framework) assembly
        /// </summary>
        private static bool IsCustomAssembly(string? assemblyName, Assembly assembly)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return false;

            // Exclude system/framework assemblies by prefix
            if (ExcludedAssemblyPrefixes.Any(prefix =>
                assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Exclude dynamic assemblies
            if (assembly.IsDynamic)
                return false;

            // Only include assemblies from the application base directory
            if (!TryGetAssemblyLocation(assembly, out string? location))
                return false;

            string baseDirectory = EnsureTrailingDirectorySeparator(AppDomain.CurrentDomain.BaseDirectory);
            return location.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Safely attempts to get the assembly location
        /// </summary>
        private static bool TryGetAssemblyLocation(Assembly assembly, out string? location)
        {
            location = null;
            try
            {
                location = assembly.Location;
                return !string.IsNullOrEmpty(location);
            }
            catch (NotSupportedException ex)
            {
                if (Log.IsDebugEnabled)
                    Log.Debug($"Cannot get location for assembly '{assembly.FullName}' (may be dynamic): {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                if (Log.IsWarnEnabled)
                    Log.Warn($"Error getting location for assembly '{assembly.FullName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ensures a path ends with a directory separator
        /// </summary>
        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            char lastChar = path[^1];
            if (lastChar != Path.DirectorySeparatorChar && lastChar != Path.AltDirectorySeparatorChar)
                return path + Path.DirectorySeparatorChar;

            return path;
        }

        /// <summary>
        /// Safely loads types from an assembly
        /// </summary>
        private bool TryLoadAssemblyTypes(Assembly assembly, out Type[] types)
        {
            if (_assemblyTypeCache.TryGetValue(assembly, out types!))
                return true;

            try
            {
                types = assembly.GetTypes();
                _assemblyTypeCache.TryAdd(assembly, types);
                return true;
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.OfType<Type>().ToArray();
                _assemblyTypeCache.TryAdd(assembly, types);
                Log.Error($"Failed to load types from assembly '{assembly.FullName}': {ex.Message}", ex);
                return true;
            }
            catch (Exception ex)
            {
                types = Array.Empty<Type>();
                Log.Error($"Unexpected error loading types from assembly '{assembly.FullName}': {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets the cached loadable types for an application assembly.
        /// Assembly metadata is immutable after load, so sharing this snapshot avoids
        /// repeating RuntimeModule.GetTypes across every startup discovery surface.
        /// </summary>
        public IReadOnlyList<Type> GetTypes(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            return TryLoadAssemblyTypes(assembly, out Type[] types)
                ? types
                : Array.Empty<Type>();
        }

        /// <summary>
        /// Refreshes and returns the filtered list of custom assemblies
        /// </summary>
        public Assembly[] RefreshAssemblies()
        {
            Assembly[] customAssemblies;
            bool changed;
            lock (_assembliesLock)
            {
                IEnumerable<Assembly> candidates = _registeredAssembliesOnly
                    ? _registeredAssemblies
                    : AppDomain.CurrentDomain.GetAssemblies().Concat(_registeredAssemblies);
                customAssemblies = candidates
                    .Distinct()
                    .Where(assembly =>
                    {
                        string? name = assembly.GetName().Name;
                        return IsCustomAssembly(name, assembly) && TryLoadAssemblyTypes(assembly, out _);
                    })
                    .ToArray();

                changed = _assemblies != null && !_assemblies.ToHashSet().SetEquals(customAssemblies);
                _assemblies = customAssemblies;

                // Clear cache when assemblies are refreshed
                Volatile.Write(ref _implementationTypeCache, new());

                if (Log.IsDebugEnabled)
                    Log.Debug($"Loaded {customAssemblies.Length} custom assemblies");
            }

            if (changed)
                AssembliesChanged?.Invoke(this, EventArgs.Empty);
            return customAssemblies;
        }

        /// <summary>
        /// Gets the cached list of custom assemblies, loading them if necessary
        /// </summary>
        public Assembly[] GetAssemblies()
        {
            if (_assemblies != null)
                return _assemblies;

            return RefreshAssemblies();
        }

        /// <summary>
        /// Loads and instantiates all implementations of the specified interface type
        /// </summary>
        /// <typeparam name="T">The interface type to find implementations for</typeparam>
        /// <param name="args">Optional constructor arguments (currently unused)</param>
        /// <returns>List of instantiated implementations</returns>
        /// <exception cref="ArgumentException">Thrown when T is not an interface type</exception>
        public List<T> LoadImplementations<T>(params object?[]? args) where T : class
        {
            var interfaceType = typeof(T);

            if (!interfaceType.IsInterface)
            {
                throw new ArgumentException(
                    $"Type parameter T must be an interface. Provided type: '{interfaceType.FullName}'",
                    nameof(T));
            }

            var implementationTypes = GetImplementationTypes(interfaceType)
                .Where(type => type.GetConstructor(Type.EmptyTypes) != null);
            return InstantiateTypes<T>(implementationTypes, args);
        }

        /// <summary>
        /// Gets concrete, closed classes assignable to the service type from the shared cache.
        /// The cache is cleared whenever the discovered assembly set changes.
        /// </summary>
        public IReadOnlyList<Type> GetImplementationTypes(Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            // An in-flight query may finish after registration or a refresh. Keep its result
            // in the old snapshot rather than repopulating the current cache with stale types.
            return Volatile.Read(ref _implementationTypeCache).GetOrAdd(serviceType, key =>
            {
                var implementationTypes = new List<Type>();
                var assemblies = GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    implementationTypes.AddRange(GetTypes(assembly)
                        .Where(t => IsConcreteImplementation(t, key)));
                }

                if (Log.IsDebugEnabled)
                    Log.Debug($"Found {implementationTypes.Count} implementations of '{key.Name}'");

                return implementationTypes.ToArray();
            });
        }

        private static bool IsConcreteImplementation(Type type, Type serviceType)
        {
            return type.IsClass
                   && !type.IsAbstract
                   && !type.ContainsGenericParameters
                   && serviceType.IsAssignableFrom(type);
        }

        /// <summary>
        /// Instantiates a list of types
        /// </summary>
        private static List<T> InstantiateTypes<T>(IEnumerable<Type> types, params object?[]? args) where T : class
        {
            var instances = new List<T>();

            foreach (var type in types)
            {
                try
                {
                    // Note: args parameter is currently unused but kept for future extensibility
                    var instance = Activator.CreateInstance(type) as T;
                    if (instance != null)
                    {
                        instances.Add(instance);
                    }
                    else
                    {
                        Log.Warn($"Created instance of '{type.FullName}' but it could not be cast to '{typeof(T).Name}'");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to create instance of '{type.FullName}': {ex.Message}", ex);
                }
            }

            return instances;
        }

        /// <summary>
        /// Clears all internal caches
        /// </summary>
        public void ClearCaches()
        {
            lock (_assembliesLock)
            {
                _assemblies = null;
                _assemblyTypeCache.Clear();
                Volatile.Write(ref _implementationTypeCache, new());
            }

            AssembliesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

}
