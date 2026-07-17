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
        /// <summary>Stable identity used for replacement and live updates.</summary>
        string Id => GetType().FullName ?? GetType().Name;

        /// <summary>The provider expected to load the project created by this template.</summary>
        string? ProjectProviderId => null;

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
    public sealed class ProjectTemplateAttribute : Attribute
    {
        public int Priority { get; }
        public ProjectTemplateAttribute(int priority = 0) => Priority = priority;
    }

    public static class ProjectTemplateRegistry
    {
        private sealed record Registration(IProjectTemplate Template, int Priority);

        private static readonly List<Registration> _templates = new();
        private static readonly HashSet<Assembly> _registeredAssemblies = new();
        private static readonly object _syncRoot = new();
        private static bool _initialized;
        private static bool _assemblyLoadSubscribed;

        public static event EventHandler? TemplatesChanged;

        public static void Initialize()
        {
            if (_initialized)
                return;

            bool changed = false;
            lock (_syncRoot)
            {
                if (_initialized)
                    return;

                if (!_assemblyLoadSubscribed)
                {
                    AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
                    _assemblyLoadSubscribed = true;
                }

                Assembly[] assemblies = AssemblyService.Instance?.GetAssemblies()
                    ?? AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                    changed |= RegisterTemplatesFromAssemblyCore(assembly);
                _initialized = true;
            }

            if (changed)
                TemplatesChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Register(IProjectTemplate template, int priority = 0)
        {
            ValidateTemplate(template);
            bool changed;
            lock (_syncRoot)
                changed = RegisterCore(template, priority, replaceEqualPriority: true);
            if (changed)
                TemplatesChanged?.Invoke(null, EventArgs.Empty);
        }

        public static bool Unregister(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
                return false;

            bool changed;
            lock (_syncRoot)
                changed = _templates.RemoveAll(item => string.Equals(item.Template.Id, templateId, StringComparison.OrdinalIgnoreCase)) > 0;
            if (changed)
                TemplatesChanged?.Invoke(null, EventArgs.Empty);
            return changed;
        }

        private static void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs e)
        {
            bool changed;
            lock (_syncRoot)
                changed = RegisterTemplatesFromAssemblyCore(e.LoadedAssembly);
            if (changed)
                TemplatesChanged?.Invoke(null, EventArgs.Empty);
        }

        private static bool RegisterTemplatesFromAssemblyCore(Assembly assembly)
        {
            if (!_registeredAssemblies.Add(assembly))
                return false;

            bool changed = false;
            foreach (Type type in GetLoadableTypes(assembly))
            {
                var attribute = type.GetCustomAttribute<ProjectTemplateAttribute>();
                if (attribute == null
                    || !typeof(IProjectTemplate).IsAssignableFrom(type)
                    || type.IsInterface
                    || type.IsAbstract)
                {
                    continue;
                }

                try
                {
                    var template = (IProjectTemplate)Activator.CreateInstance(type)!;
                    ValidateTemplate(template);
                    changed |= RegisterCore(template, attribute.Priority, replaceEqualPriority: false);
                }
                catch
                {
                }
            }
            return changed;
        }

        private static bool RegisterCore(IProjectTemplate template, int priority, bool replaceEqualPriority)
        {
            int existingIndex = _templates.FindIndex(item => string.Equals(
                item.Template.Id,
                template.Id,
                StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                Registration existing = _templates[existingIndex];
                if (existing.Priority > priority || (!replaceEqualPriority && existing.Priority == priority))
                    return false;
                _templates.RemoveAt(existingIndex);
            }
            _templates.Add(new Registration(template, priority));
            return true;
        }

        private static void ValidateTemplate(IProjectTemplate template)
        {
            ArgumentNullException.ThrowIfNull(template);
            if (string.IsNullOrWhiteSpace(template.Id))
                throw new ArgumentException("项目模板 Id 不允许为空。", nameof(template));
            if (string.IsNullOrWhiteSpace(template.Name))
                throw new ArgumentException("项目模板名称不允许为空。", nameof(template));
            if (string.IsNullOrWhiteSpace(template.Category))
                throw new ArgumentException("项目模板分类不允许为空。", nameof(template));
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null)!;
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }

        public static IReadOnlyList<IProjectTemplate> GetTemplates()
        {
            Initialize();
            lock (_syncRoot)
            {
                return _templates
                    .OrderBy(item => item.Template.Category, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(item => item.Template.Order)
                    .ThenByDescending(item => item.Priority)
                    .ThenBy(item => item.Template.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(item => item.Template)
                    .ToList();
            }
        }

        public static IReadOnlyDictionary<string, List<IProjectTemplate>> GetTemplatesByCategory()
        {
            return GetTemplates()
                .GroupBy(template => template.Category)
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        /// <summary>
        /// Create a project from a template.
        /// Creates the directory and calls the template's CreateProject method.
        /// </summary>
        public static DirectoryInfo? CreateFromTemplate(IProjectTemplate template, string parentDir, string projectName)
        {
            return TryCreateFromTemplate(template, parentDir, projectName, out DirectoryInfo? projectDirectory, out _)
                ? projectDirectory
                : null;
        }

        public static bool TryCreateFromTemplate(
            IProjectTemplate template,
            string parentDir,
            string projectName,
            out DirectoryInfo? projectDirectory,
            out string errorMessage)
        {
            projectDirectory = null;
            errorMessage = string.Empty;
            try
            {
                ValidateTemplate(template);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                errorMessage = $"项目模板无效：{ex.Message}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(parentDir) || !Directory.Exists(parentDir))
            {
                errorMessage = "项目保存目录不存在。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(projectName)
                || projectName is "." or ".."
                || projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || !string.Equals(Path.GetFileName(projectName), projectName, StringComparison.Ordinal))
            {
                errorMessage = "项目名称为空或包含无效字符。";
                return false;
            }

            string normalizedParent;
            string projectPath;
            try
            {
                normalizedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parentDir));
                projectPath = Path.GetFullPath(Path.Combine(normalizedParent, projectName));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errorMessage = $"项目路径无效：{ex.Message}";
                return false;
            }

            if (!string.Equals(Path.GetDirectoryName(projectPath), normalizedParent, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "项目必须创建在所选解决方案目录的直属子目录中。";
                return false;
            }
            if (Directory.Exists(projectPath) || File.Exists(projectPath))
            {
                errorMessage = $"项目“{projectName}”已存在。";
                return false;
            }

            bool createdDirectory = false;
            try
            {
                Directory.CreateDirectory(projectPath);
                createdDirectory = true;
                template.CreateProject(projectPath, projectName);

                var directory = new DirectoryInfo(projectPath);
                if (!ProjectProviderRegistry.TryLoadProject(directory, out ProjectDefinition? project, out string loadError)
                    || project == null)
                {
                    errorMessage = $"模板没有创建可加载的项目：{loadError}";
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(template.ProjectProviderId)
                    && !string.Equals(template.ProjectProviderId, project.ProviderId, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"模板要求 Provider“{template.ProjectProviderId}”，实际由“{project.ProviderId}”加载。";
                    return false;
                }

                projectDirectory = directory;
                createdDirectory = false;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"创建项目失败：{ex.Message}";
                return false;
            }
            finally
            {
                if (createdDirectory)
                    TryRemoveFailedProjectDirectory(projectPath, ref errorMessage);
            }
        }

        private static void TryRemoveFailedProjectDirectory(string projectPath, ref string errorMessage)
        {
            try
            {
                if (Directory.Exists(projectPath))
                    Directory.Delete(projectPath, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errorMessage += $" 清理未完成的项目目录失败：{ex.Message}";
            }
        }
    }
}
