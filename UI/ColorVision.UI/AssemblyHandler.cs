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
            return AssemblyHandler.Instance.RefreshAssemblies();
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
            "Newtonsoft", "EntityFramework", "log4net", "Wpf.Ui", "HandyControl"
        };

        private Assembly[]? _assemblies;
        private readonly ConcurrentDictionary<Type, List<Type>> _implementationTypeCache = new();
        private readonly object _assembliesLock = new();

        private AssemblyHandler()
        {
            AssemblyService.SetInstance(this);
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
        private static bool TryLoadAssemblyTypes(Assembly assembly)
        {
            try
            {
                _ = assembly.GetTypes();
                return true;
            }
            catch (ReflectionTypeLoadException ex)
            {
                Log.Error($"Failed to load types from assembly '{assembly.FullName}': {ex.Message}", ex);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error loading types from assembly '{assembly.FullName}': {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Refreshes and returns the filtered list of custom assemblies
        /// </summary>
        public Assembly[] RefreshAssemblies()
        {
            lock (_assembliesLock)
            {
                var customAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly =>
                    {
                        string? name = assembly.GetName().Name;
                        return IsCustomAssembly(name, assembly) && TryLoadAssemblyTypes(assembly);
                    })
                    .ToArray();

                _assemblies = customAssemblies;

                // Clear cache when assemblies are refreshed
                _implementationTypeCache.Clear();

                if (Log.IsDebugEnabled)
                    Log.Debug($"Loaded {customAssemblies.Length} custom assemblies");

                return customAssemblies;
            }
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

            var implementationTypes = GetOrCacheImplementationTypes(interfaceType);
            return InstantiateTypes<T>(implementationTypes, args);
        }

        /// <summary>
        /// Gets implementation types from cache or discovers them
        /// </summary>
        private List<Type> GetOrCacheImplementationTypes(Type interfaceType)
        {
            return _implementationTypeCache.GetOrAdd(interfaceType, key =>
            {
                var implementationTypes = new List<Type>();
                var assemblies = GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var types = assembly.GetTypes()
                            .Where(t => IsInstantiableImplementation(t, key));

                        implementationTypes.AddRange(types);
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        Log.Error($"Failed to load types from assembly '{assembly.FullName}': {ex.Message}", ex);

                        // Try to salvage successfully loaded types
                        if (ex.Types != null)
                        {
                            var loadedTypes = ex.Types
                                .Where(t => t != null && IsInstantiableImplementation(t, key));
                            implementationTypes.AddRange(loadedTypes!);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error processing assembly '{assembly.FullName}': {ex.Message}", ex);
                    }
                }

                if (Log.IsDebugEnabled)
                    Log.Debug($"Found {implementationTypes.Count} implementations of '{interfaceType.Name}'");

                return implementationTypes;
            });
        }

        /// <summary>
        /// Determines if a type is a concrete implementation that can be instantiated
        /// </summary>
        private static bool IsInstantiableImplementation(Type type, Type interfaceType)
        {
            return interfaceType.IsAssignableFrom(type)
                   && type.IsClass
                   && !type.IsAbstract
                   && !type.IsInterface
                   && type.GetConstructor(Type.EmptyTypes) != null;
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
                _implementationTypeCache.Clear();
            }
        }
    }

}
