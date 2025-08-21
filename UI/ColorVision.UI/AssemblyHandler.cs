using log4net;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

namespace ColorVision.UI
{
    public class AssemblyHandler: IAssemblyService
    {
        private static ILog log = LogManager.GetLogger(typeof(AssemblyHandler));
        private static AssemblyHandler _instance;
        private static readonly object _locker = new();
        public static AssemblyHandler GetInstance()
        {
            lock (_locker)
            {
                _instance ??= new AssemblyHandler();
                AssemblyService.SetInstance(_instance);
                return _instance;
            }
        }

        private Assembly[] Assemblies { get; set; }
        /// <summary>
        /// 仅允许自定义项目的程序集
        /// </summary>
        private static bool IsCustomAssembly(string name, Assembly assembly)
        {
            if (string.IsNullOrEmpty(name)) return false;

            // 1. 排除常见的系统/框架程序集（不区分大小写）
            string[] excludedPrefixes = {
                "System", "Microsoft", "netstandard", "WindowsBase",
                "PresentationCore", "PresentationFramework", "mscorlib",
                "Newtonsoft", "EntityFramework","log4net","Wpf.Ui","HandyControl"
            };
            if (excludedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // 2. 可选：仅包含应用程序基目录中的 DLL
            string location = string.Empty;
            try
            {
                if (!assembly.IsDynamic) // 动态程序集可能没有位置或会抛出异常
                {
                    location = assembly.Location;
                }
            }
            catch (NotSupportedException nse)
            {
                // 动态程序集或某些宿主环境可能会发生这种情况。
                if (log.IsDebugEnabled)
                    log.Debug($"无法获取程序集 '{assembly.FullName}' 的位置 (可能是动态的): {nse.Message}");
            }
            catch (Exception ex) // 捕获其他潜在异常
            {
                if (log.IsWarnEnabled)
                    log.Warn($"无法获取程序集 '{assembly.FullName}' 的位置: {ex.Message}");
            }

            if (!string.IsNullOrEmpty(location))
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                // 确保 basePath 以目录分隔符结尾，以便进行可靠的 StartsWith 比较
                if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString(),StringComparison.CurrentCulture) && !basePath.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.CurrentCulture))
                {
                    basePath += Path.DirectorySeparatorChar;
                }
                if (!location.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else if (assembly.IsDynamic)
            {
                // 决定动态程序集是否应被视为“自定义”。
                // 默认情况下，如果位置无法根据 BaseDirectory 进行验证，则将其排除。
                return false;
            }
            // 如果位置为空且程序集不是动态的，则这是一个边缘情况。
            // 根据需求，可能需要特殊处理。如果未按名称排除，则默认为包含。

            // 3. 可以在此处添加进一步的自定义逻辑（例如，命名空间前缀）。
            // 示例: if (!name.StartsWith("YourCompany.", StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }


        public Assembly[] RefreshAssemblies()
        {
            List<Assembly> assemblies = new List<Assembly>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var name = assembly.GetName().Name;

                    ///这里这么些可以优化一个数量级，一个接口反射全部大概要2ms,现在只需要0.2ms ，聊胜于无
                    if (!string.IsNullOrWhiteSpace(name) && IsCustomAssembly(name, assembly))
                    {
                        assembly.GetTypes(); // try load
                        assemblies.Add(assembly);
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to unload assembly: {ex.Message}", ex);
                }
            }
            Assemblies = assemblies.ToArray();
            return Assemblies;
        }


        public Assembly[] GetAssemblies()
        {
            if (Assemblies == null)
            {
                List<Assembly> assemblies = new List<Assembly>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var name = assembly.GetName().Name;

                        ///这里这么些可以优化一个数量级，一个接口反射全部大概要2ms,现在只需要0.2ms ，聊胜于无
                        if (!string.IsNullOrWhiteSpace(name)&& IsCustomAssembly(name, assembly))
                        {
                            assembly.GetTypes(); // try load
                            assemblies.Add(assembly);
                        }
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
        private static readonly ConcurrentDictionary<Type, List<Type>> _cachedImplementingTypes = new ConcurrentDictionary<Type, List<Type>>();

        public  List<T> LoadImplementations<T>( params object?[]? args) where T : class
        {
            var targetInterfaceType = typeof(T);
            // **新增检查：确保 T 是一个接口类型**
            if (!targetInterfaceType.IsInterface)
            {
                throw new ArgumentException($"The generic type parameter T ('{targetInterfaceType.FullName}') must be an interface type.", nameof(T));
            }
            List<Type> typesToInstantiate;
            if (!_cachedImplementingTypes.TryGetValue(targetInterfaceType, out typesToInstantiate))
            {
                typesToInstantiate = new List<Type>();
                var assemblies = GetInstance().GetAssemblies(); // 获取经过筛选的程序集

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var foundTypes = assembly.GetTypes()
                                            .Where(t => targetInterfaceType.IsAssignableFrom(t) && // 类型 t 实现了接口 T
                                                        !t.IsInterface &&                         // 类型 t 本身不是接口
                                                        !t.IsAbstract &&                          // 类型 t 不是抽象类
                                                        t.GetConstructor(Type.EmptyTypes) != null); // 类型 t 有公共无参构造函数

                        typesToInstantiate.AddRange(foundTypes);
                    }
                    catch (ReflectionTypeLoadException rtle)
                    {
                        log.Error($"Failed to load one or more types from assembly '{assembly.FullName}'. Details: {rtle.Message}", rtle);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error processing assembly '{assembly.FullName}': {ex.Message}", ex);
                    }
                }
                _cachedImplementingTypes.TryAdd(targetInterfaceType, typesToInstantiate);
            }

            var instances = new List<T>();
            foreach (var type in typesToInstantiate)
            {
                try
                {
                    if (Activator.CreateInstance(type) is T instance)
                    {
                        instances.Add(instance);
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to create instance of type '{type.FullName}': {ex.Message}", ex);
                }
            }
            return instances;
        }
    }

}
