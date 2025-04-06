using System.Reflection;
using System.Runtime.Loader;

namespace ColorVision.UI
{
    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly ICollection<string> _plugInApiAssemblyNames;
        public PluginLoadContext( string pluginPath, ICollection<string> plugInApiAssemblies)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
            _plugInApiAssemblyNames = plugInApiAssemblies;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            if (!_plugInApiAssemblyNames.Contains(assemblyName.Name!))
            {
                string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
                if (assemblyPath != null)
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }
            }
            return Default.LoadFromAssemblyName(assemblyName);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
