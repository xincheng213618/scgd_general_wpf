using ColorVision.UI;
using System.IO;
using System.Reflection;
using System.Windows.Media;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// Defines a template for creating new projects in the Solution Explorer.
    /// A project is a folder with a .cvproj marker file and optional structure.
    /// Implement this interface and decorate with [ProjectTemplateAttribute] to register.
    /// </summary>
    public interface IProjectTemplate
    {
        /// <summary>Display name shown in the new project dialog</summary>
        string Name { get; }

        /// <summary>Category for grouping (e.g. "通用", "检测", "自定义")</summary>
        string Category { get; }

        /// <summary>Description shown when the template is selected</summary>
        string Description { get; }

        /// <summary>Menu display order within category</summary>
        int Order { get; }

        /// <summary>Optional icon for the template</summary>
        ImageSource? Icon { get; }

        /// <summary>
        /// Create the project structure in the given directory.
        /// The directory will already exist when this method is called.
        /// Should create the .cvproj file and any initial files/folders.
        /// </summary>
        /// <param name="projectDir">The project directory (already created)</param>
        /// <param name="projectName">The user-specified project name</param>
        void CreateProject(string projectDir, string projectName);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ProjectTemplateAttribute : Attribute
    {
        public int Priority { get; }
        public ProjectTemplateAttribute(int priority = 0) => Priority = priority;
    }

    public static class ProjectTemplateRegistry
    {
        private static readonly List<IProjectTemplate> _templates = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IProjectTemplate).IsAssignableFrom(type)
                        && !type.IsInterface && !type.IsAbstract
                        && type.GetCustomAttribute<ProjectTemplateAttribute>() != null)
                    {
                        try
                        {
                            var instance = (IProjectTemplate)Activator.CreateInstance(type)!;
                            _templates.Add(instance);
                        }
                        catch { }
                    }
                }
            }
        }

        public static IReadOnlyList<IProjectTemplate> GetTemplates()
        {
            return _templates.OrderBy(t => t.Category).ThenBy(t => t.Order).ToList();
        }

        public static IReadOnlyDictionary<string, List<IProjectTemplate>> GetTemplatesByCategory()
        {
            return _templates
                .GroupBy(t => t.Category)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Order).ToList());
        }

        /// <summary>
        /// Create a project from a template.
        /// Creates the directory and calls the template's CreateProject method.
        /// </summary>
        public static DirectoryInfo? CreateFromTemplate(IProjectTemplate template, string parentDir, string projectName)
        {
            try
            {
                string projectDir = Path.Combine(parentDir, projectName);
                int count = 2;
                while (Directory.Exists(projectDir))
                {
                    projectDir = Path.Combine(parentDir, $"{projectName}({count})");
                    count++;
                }

                Directory.CreateDirectory(projectDir);
                template.CreateProject(projectDir, projectName);
                return new DirectoryInfo(projectDir);
            }
            catch
            {
                return null;
            }
        }
    }
}
