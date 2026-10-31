using ColorVision.UI;
using System.Reflection;

namespace ColorVision.Solution
{
    /// <summary>
    /// Where the Solution registries discover extensions: the host's assembly service (the module
    /// catalog in the main application) when present, otherwise every loaded assembly.
    /// </summary>
    internal static class SolutionAssemblyDiscovery
    {
        public static IEnumerable<Assembly> GetAssemblies()
            => AssemblyService.Instance?.GetAssemblies() ?? AppDomain.CurrentDomain.GetAssemblies();

        public static IEnumerable<Type> GetTypes(Assembly assembly)
        {
            if (AssemblyService.Instance is { } service)
                return service.GetTypes(assembly);

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.OfType<Type>();
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// Invokes <paramref name="onAssembliesChanged"/> with assemblies that may be new. Registries
        /// deduplicate by assembly, so receiving an already registered assembly again is harmless.
        /// </summary>
        public static void Subscribe(Action<IEnumerable<Assembly>> onAssembliesChanged)
        {
            if (AssemblyService.Instance is { } service)
                service.AssembliesChanged += (_, _) => onAssembliesChanged(service.GetAssemblies());
            else
                AppDomain.CurrentDomain.AssemblyLoad += (_, e) => onAssembliesChanged([e.LoadedAssembly]);
        }
    }
}
