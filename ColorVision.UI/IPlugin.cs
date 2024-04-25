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

    public class PluginLoader
    {
        public static List<IPlugin> LoadPlugins(string path)
        {
            List<IPlugin> plugins = new List<IPlugin>();
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
