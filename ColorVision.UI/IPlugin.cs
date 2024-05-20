using System.IO;
using System.Reflection;

namespace ColorVision.UI
{

    public interface IPlugin
    {
        public string Name { get; }
        public string Description { get; }
        void Execute();
    }

    public static class PluginLoader
    {
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

        public static List<Assembly> PluginAssembly { get; } = new List<Assembly>();

        public static List<Assembly> LoadPluginsAssembly(string path)
        {
            if (!Directory.Exists(path)) return PluginAssembly;
            // 获取所有 dll 文件
            foreach (string file in Directory.GetFiles(path, "*.dll"))
            {
                Assembly assembly = Assembly.LoadFrom(file);
                PluginAssembly.Add(assembly);
            }
            return PluginAssembly;
        }

        public static List<IPlugin> LoadPlugins(string path)
        {
            List<IPlugin> plugins = new();
            if (!Directory.Exists(path)) return plugins;
            // 获取所有 dll 文件
            foreach (string file in Directory.GetFiles(path, "*.dll"))
            {
                Assembly assembly = Assembly.LoadFrom(file);
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.GetInterfaces().Contains(typeof(IPlugin)))
                    {
                        if (Activator.CreateInstance(type) is IPlugin plugin)
                        {
                            plugin.Execute();
                            plugins.Add(plugin);
                        }
                    }
                }
            }
            return plugins;
        }
    }
}
