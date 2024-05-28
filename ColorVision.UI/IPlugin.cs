using ColorVision.Common.MVVM;
using log4net;
using log4net.Plugin;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
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

            return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);

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

    public interface IPlugin
    {
        public string Name { get; }
        public string Description { get; }
        void Execute();
    }

    public static class PluginLoader
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginLoader));

        public static List<T> LoadAssembly<T>(Assembly assembly)where T: IPlugin
        {
            List<T> plugins = new();
            foreach (Type type in assembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract))
            {
                if (Activator.CreateInstance(type) is T plugin)
                {
                    plugin.Execute();
                    plugins.Add(plugin);
                }
            }
            return plugins;
        }
        public static PluginLoadContext loadContext { get; set; }

        public static void LoadPluginsUS(string path)
        {
            Assembly[] plugInApiAssemblies =  {typeof(IPlugin).Assembly,typeof(RelayCommand).Assembly };
            var plugInAssemblyNames = new HashSet<string>( plugInApiAssemblies.Select(a => a.GetName().Name!));
            loadContext = new PluginLoadContext(path, plugInAssemblyNames);

            // 获取所有 dll 文件
            foreach (string file in Directory.GetFiles(path, "*.dll"))
            {
                try
                {
                    string absolutePath = Path.GetFullPath(file); // 确保路径是绝对路径
                    var assembly = loadContext.LoadFromAssemblyPath(absolutePath);
                    foreach (var type in assembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type) is IPlugin plugin)
                        {
                            plugin.Execute();
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }


            }
        }

        public static void UnloadPlugins()
        {
            loadContext?.Unload();
            loadContext = null;
        }

        public static void LoadPluginsAssembly(string path)
        {
            string DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\{path}";
            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            // 获取所有 dll 文件
            foreach (string file in Directory.GetFiles(DirectoryPath, "*.dll"))
            {
                try
                {
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(file);
                    string version = versionInfo.FileVersion;
                    byte[] assemblyBytes = File.ReadAllBytes(file);
                    Assembly assembly = Assembly.Load(assemblyBytes); 
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }

            if (!Directory.Exists(path)) 
                return ;
            // 获取所有 dll 文件
            foreach (string file in Directory.GetFiles(path, "*.dll"))
            {
                try
                {
                    Assembly assembly = Assembly.LoadFrom(file); ;
                }
                catch(Exception ex)
                {
                    log.Error(ex);
                }
            }
        }

        public static List<IPlugin> LoadPlugins(string path)
        {
            List<IPlugin> plugins = new();
            if (!Directory.Exists(path)) return plugins;
            // 获取所有 dll 文件
            foreach (string file in Directory.GetFiles(path, "*.dll"))
            {
                try
                {
                    Assembly assembly = Assembly.LoadFrom(file); ;
                    foreach (var type in assembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type) is IPlugin plugin)
                            {
                                plugin.Execute();
                                plugins.Add(plugin);
                            }
                    }
                }catch(Exception ex)
                {
                    log.Error(ex);
                }

            }
            return plugins;
        }
    }
}
