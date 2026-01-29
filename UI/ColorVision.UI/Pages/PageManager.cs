#pragma warning disable CS8604
using System.Collections.Concurrent;
using System.Reflection;
using System.Windows.Controls;

namespace ColorVision.UI.Pages
{
    public class PageManager
    {
        private static readonly Lazy<PageManager> _instance = new Lazy<PageManager>(() => new PageManager());
        public static PageManager Instance => _instance.Value;

        public ConcurrentDictionary<string, Type> Pages { get; } = new ConcurrentDictionary<string, Type>();

        public PageManager()
        {
            var assemblies = AssemblyHandler.GetInstance().GetAssemblies();
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IPage).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        // 优先从特性获取标题，避免实例化
                        var titleAttr = type.GetCustomAttribute<PageAttribute>();
                        string pageTitle = titleAttr?.Title ?? type.Name; // 如果没有特性，回退到实例化获取

                        if (!string.IsNullOrEmpty(pageTitle))
                        {
                            if (!Pages.TryAdd(pageTitle, type))
                            {
                                // 处理重复标题（例如记录日志或抛异常）
                                Console.WriteLine($"Warning: Duplicate PageTitle '{pageTitle}' for type {type.FullName}");
                            }
                        }
                    }
                }
            }
        }
        public Page GetPage(string? pageTitle, Frame frame)
        {
            if (Pages.TryGetValue(pageTitle, out Type type))
            {
                if (Activator.CreateInstance(type, frame) is Page page)
                {
                    return page;
                }
            }
            return new Page();
        }
    }
}
