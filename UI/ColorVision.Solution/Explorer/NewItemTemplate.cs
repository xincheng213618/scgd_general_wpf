using ColorVision.UI;
using System.IO;
using System.Reflection;
using System.Windows.Media;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// Defines a template for creating new files/folders in the Solution Explorer.
    /// Implement this interface and decorate with [NewItemTemplateAttribute] to register.
    /// Plugins can contribute templates by implementing this interface.
    /// </summary>
    public interface INewItemTemplate
    {
        /// <summary>Display name shown in the "Add" menu</summary>
        string Name { get; }

        /// <summary>Category for grouping in the menu (e.g. "Script", "Config", "Data")</summary>
        string Category { get; }

        /// <summary>File extension including dot (e.g. ".py", ".ps1", ".json"). Null for folders.</summary>
        string? Extension { get; }

        /// <summary>Menu display order within category</summary>
        int Order { get; }

        /// <summary>Optional icon for the menu item</summary>
        ImageSource? Icon { get; }

        /// <summary>
        /// Generate the default content for a new file.
        /// Return null for binary files or folders.
        /// </summary>
        /// <param name="fileName">The target file name (without path)</param>
        string? GetDefaultContent(string fileName);

        /// <summary>
        /// Generate the default file name (without extension).
        /// Return null to use the template Name as default.
        /// </summary>
        string? GetDefaultFileName() => null;
    }

    /// <summary>
    /// Attribute to mark INewItemTemplate implementations for auto-discovery
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class NewItemTemplateAttribute : Attribute
    {
        public int Priority { get; }
        public NewItemTemplateAttribute(int priority = 0) => Priority = priority;
    }

    /// <summary>
    /// Registry for discovering and managing new item templates.
    /// Templates are discovered via [NewItemTemplateAttribute] on INewItemTemplate implementations.
    /// </summary>
    public static class NewItemTemplateRegistry
    {
        private static readonly List<INewItemTemplate> _templates = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(INewItemTemplate).IsAssignableFrom(type)
                        && !type.IsInterface && !type.IsAbstract
                        && type.GetCustomAttribute<NewItemTemplateAttribute>() != null)
                    {
                        try
                        {
                            var instance = (INewItemTemplate)Activator.CreateInstance(type)!;
                            _templates.Add(instance);
                        }
                        catch
                        {
                            // Skip templates that fail to instantiate
                        }
                    }
                }
            }
        }

        /// <summary>Get all registered templates, ordered by category then order</summary>
        public static IReadOnlyList<INewItemTemplate> GetTemplates()
        {
            return _templates
                .OrderBy(t => t.Category)
                .ThenBy(t => t.Order)
                .ToList();
        }

        /// <summary>Get templates grouped by category</summary>
        public static IReadOnlyDictionary<string, List<INewItemTemplate>> GetTemplatesByCategory()
        {
            return _templates
                .GroupBy(t => t.Category)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Order).ToList());
        }

        /// <summary>
        /// Create a file from a template in the given directory.
        /// Returns the created FileInfo, or null on failure.
        /// </summary>
        public static FileInfo? CreateFromTemplate(INewItemTemplate template, string directoryPath)
        {
            try
            {
                string baseName = template.GetDefaultFileName() ?? template.Name;
                string ext = template.Extension ?? "";
                string fileName = baseName + ext;
                string fullPath = Path.Combine(directoryPath, fileName);

                int count = 2;
                while (File.Exists(fullPath))
                {
                    fullPath = Path.Combine(directoryPath, $"{baseName}({count}){ext}");
                    count++;
                }

                string? content = template.GetDefaultContent(Path.GetFileName(fullPath));
                if (content != null)
                    File.WriteAllText(fullPath, content);
                else
                    File.Create(fullPath).Dispose();

                return new FileInfo(fullPath);
            }
            catch
            {
                return null;
            }
        }
    }
}
