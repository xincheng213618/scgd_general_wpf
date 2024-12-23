using ColorVision.Common.MVVM;
using log4net;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI
{
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
                    plugins.Add(plugin);
                }
            }
            return plugins;
        }

        public static PluginLoadContext? loadContext { get; set; }

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
            if (!Directory.Exists(path)) 
                return ;
            foreach (var directory in Directory.GetDirectories(path))
            {
                string directoryName = Path.GetFileName(directory);
                string dllPath = Path.Combine(directory, directoryName + ".dll");

                try
                {
                    Assembly assembly = Assembly.LoadFrom(dllPath); ;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("加载插件错误：" + ex.Message, "ColorVision");
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
                    using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        byte[] assemblyData = new byte[fs.Length];
                        fs.Read(assemblyData, 0, assemblyData.Length);
                        Assembly assembly = Assembly.Load(assemblyData);
                        foreach (var type in assembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract))
                        {
                            if (Activator.CreateInstance(type) is IPlugin plugin)
                            {
                                plugin.Execute();
                                plugins.Add(plugin);
                            }
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
