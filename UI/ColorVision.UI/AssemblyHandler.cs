using log4net;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ColorVision.UI
{
    public static class AssemblyHandlerExtension
    {
        public static void LoadImplementations<T>(this ObservableCollection<T> interfaces)
        {
            interfaces.Clear();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is T imageEditorFunction)
                    {
                        interfaces.Add(imageEditorFunction);
                    }
                }
            }
        }
    }
    public class AssemblyHandler
    {
        private static ILog log = LogManager.GetLogger(typeof(AssemblyHandler));
        private static AssemblyHandler _instance;
        private static readonly object _locker = new();
        public static AssemblyHandler GetInstance()
        {
            lock (_locker)
            {
                _instance ??= new AssemblyHandler();
                return _instance;
            }
        }

        private Assembly[] Assemblies { get; set; }

        public Assembly[] GetAssemblies()
        {

            if (Assemblies == null)
            {
                List<Assembly> assemblies = new List<Assembly>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        assembly.GetTypes();
                        assemblies.Add(assembly);
                    }
                    catch(Exception ex)
                    {
                        log.Error($"Failed to unload assembly: {ex.Message}", ex);
                    }
                }
                Assemblies = assemblies.ToArray();
            }
            return Assemblies;
        }


        public List<Assembly> RemoveAssemblies { get; set; } = new List<Assembly>();
        public List<string> RemoveAssemblyNames { get; set; } = new List<string>();

        public static List<T> LoadImplementations<T>() where T : class
        {
            var instances = new List<T>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is T instance)
                    {
                        instances.Add(instance);
                    }
                }
            }

            return instances;
        }
    }

}
