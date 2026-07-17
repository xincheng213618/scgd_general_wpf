#pragma warning disable CA1860,CS8601
using ColorVision.Solution.Editor;
using ColorVision.UI;
using System.IO;
using System.Reflection;

namespace ColorVision.Solution
{
    public class EditorManagerConfig : IConfig
    {
        public static EditorManagerConfig Instance => ConfigService.Instance.GetRequiredService<EditorManagerConfig>();

        /// <summary>
        /// Maps a normalized extension to a stable editor id. Values written by
        /// older versions may still contain a type full name and are migrated on use.
        /// </summary>
        public Dictionary<string, string> DefaultEditors { get; set; } = new();

        /// <summary>
        /// Stable folder editor id. Older type full names remain readable.
        /// </summary>
        public string? DefaultFolderEditor { get; set; }
    }

    /// <summary>
    /// Discovers editor registrations and resolves a deterministic editor for a resource.
    /// Attribute-based registrations are compatibility input; consumers use descriptors.
    /// </summary>
    public class EditorManager
    {
        public static EditorManager Instance { get; } = new();

        private readonly Dictionary<string, List<EditorDescriptor>> _fileEditorsByExtension = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<EditorDescriptor> _genericFileEditors = new();
        private readonly List<EditorDescriptor> _folderEditors = new();
        private readonly Dictionary<string, EditorDescriptor> _configuredFileEditors = new(StringComparer.OrdinalIgnoreCase);
        private EditorDescriptor? _configuredFolderEditor;
        private bool _configurationLoaded;

        private EditorManager()
        {
            RegisterEditors();
            LoadConfiguredEditors();
        }

        internal static string NormalizeExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return string.Empty;

            string normalized = extension.Trim();
            if (!normalized.StartsWith('.'))
                normalized = "." + normalized;
            return normalized.ToLowerInvariant();
        }

        private void RegisterEditors()
        {
            Assembly[] assemblies = AssemblyService.Instance?.GetAssemblies()
                ?? AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (!typeof(IEditor).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract)
                        continue;

                    if (type.GetCustomAttribute<EditorForExtensionAttribute>() is { } extensionAttribute)
                    {
                        var extensions = extensionAttribute.Extensions
                            .Select(NormalizeExtension)
                            .Where(extension => extension.Length > 0)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        var descriptor = new EditorDescriptor(
                            GetEditorId(type, extensionAttribute.EditorId),
                            type,
                            EditorResourceKind.File,
                            extensions,
                            IsGeneric: false,
                            extensionAttribute.IsDefault,
                            extensionAttribute.Priority,
                            extensionAttribute.IsVisibleInOpenWith);

                        foreach (string extension in extensions)
                        {
                            if (!_fileEditorsByExtension.TryGetValue(extension, out var descriptors))
                            {
                                descriptors = new List<EditorDescriptor>();
                                _fileEditorsByExtension[extension] = descriptors;
                            }
                            AddUnique(descriptors, descriptor);
                        }
                    }

                    if (type.GetCustomAttribute<GenericEditorAttribute>() is { } genericAttribute)
                    {
                        AddUnique(_genericFileEditors, new EditorDescriptor(
                            GetEditorId(type, genericAttribute.EditorId),
                            type,
                            EditorResourceKind.File,
                            Array.Empty<string>(),
                            IsGeneric: true,
                            genericAttribute.IsDefault,
                            genericAttribute.Priority,
                            genericAttribute.IsVisibleInOpenWith));
                    }

                    if (type.GetCustomAttribute<FolderEditorAttribute>() is { } folderAttribute)
                    {
                        AddUnique(_folderEditors, new EditorDescriptor(
                            GetEditorId(type, folderAttribute.EditorId),
                            type,
                            EditorResourceKind.Folder,
                            Array.Empty<string>(),
                            IsGeneric: false,
                            folderAttribute.IsDefault,
                            folderAttribute.Priority,
                            folderAttribute.IsVisibleInOpenWith));
                    }
                }
            }
        }

        private void LoadConfiguredEditors()
        {
            if (_configurationLoaded || ConfigService.Instance == null)
                return;

            EditorManagerConfig config = ConfigService.Instance.GetRequiredService<EditorManagerConfig>();
            foreach (var pair in config.DefaultEditors)
            {
                string extension = NormalizeExtension(pair.Key);
                if (extension.Length == 0)
                    continue;

                var descriptor = FindStoredDescriptor(GetFileEditorDescriptors(extension), pair.Value);
                if (descriptor != null)
                    _configuredFileEditors[extension] = descriptor;
            }

            if (!string.IsNullOrWhiteSpace(config.DefaultFolderEditor))
                _configuredFolderEditor = FindStoredDescriptor(GetFolderEditorDescriptors(), config.DefaultFolderEditor);
            _configurationLoaded = true;
        }

        private static string GetEditorId(Type type, string? registeredId)
        {
            return string.IsNullOrWhiteSpace(registeredId)
                ? type.FullName ?? type.Name
                : registeredId.Trim();
        }

        private static void AddUnique(List<EditorDescriptor> descriptors, EditorDescriptor descriptor)
        {
            if (descriptors.Any(item => string.Equals(item.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase)))
                return;
            descriptors.Add(descriptor);
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null)!;
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }

        private static List<EditorDescriptor> OrderDescriptors(IEnumerable<EditorDescriptor> descriptors)
        {
            return descriptors
                .OrderByDescending(descriptor => descriptor.IsDefault)
                .ThenByDescending(descriptor => descriptor.Priority)
                .ThenBy(descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static EditorDescriptor? FindStoredDescriptor(IEnumerable<EditorDescriptor> descriptors, string? storedValue)
        {
            if (string.IsNullOrWhiteSpace(storedValue))
                return null;

            return descriptors.FirstOrDefault(descriptor =>
                string.Equals(descriptor.Id, storedValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(descriptor.EditorType.FullName, storedValue, StringComparison.Ordinal));
        }

        internal static EditorDescriptor? SelectDefaultFileEditor(
            IEnumerable<EditorDescriptor> specializedEditors,
            IEnumerable<EditorDescriptor> genericEditors,
            string? storedValue = null)
        {
            var specialized = OrderDescriptors(specializedEditors);
            var generic = OrderDescriptors(genericEditors);
            var configured = FindStoredDescriptor(specialized.Concat(generic), storedValue);
            if (configured != null)
                return configured;

            return specialized.FirstOrDefault(descriptor => descriptor.IsDefault)
                ?? (specialized.Count > 0 ? specialized[0] : null)
                ?? generic.FirstOrDefault(descriptor => descriptor.IsDefault)
                ?? (generic.Count > 0 ? generic[0] : null);
        }

        public static string GetEditorName(Type type)
        {
            if (type.GetCustomAttribute<EditorForExtensionAttribute>() is { } extensionAttribute)
            {
                string? name = GetLocalizedName(extensionAttribute.ResourceKey, extensionAttribute.Name);
                if (name != null)
                    return name;
            }

            if (type.GetCustomAttribute<GenericEditorAttribute>() is { } genericAttribute)
            {
                string? name = GetLocalizedName(genericAttribute.ResourceKey, genericAttribute.Name);
                if (name != null)
                    return name;
            }

            if (type.GetCustomAttribute<FolderEditorAttribute>() is { } folderAttribute)
            {
                string? name = GetLocalizedName(folderAttribute.ResourceKey, folderAttribute.Name);
                if (name != null)
                    return name;
            }

            return type.Name;
        }

        private static string? GetLocalizedName(string? resourceKey, string? fallbackName)
        {
            if (!string.IsNullOrWhiteSpace(resourceKey))
            {
                string? localized = Properties.Resources.ResourceManager.GetString(resourceKey, System.Globalization.CultureInfo.CurrentUICulture);
                if (!string.IsNullOrWhiteSpace(localized))
                    return localized;
            }
            return string.IsNullOrWhiteSpace(fallbackName) ? null : fallbackName;
        }

        public IReadOnlyList<EditorDescriptor> GetFileEditorDescriptors(string? extension, bool visibleOnly = false)
        {
            string normalizedExtension = NormalizeExtension(extension);
            IEnumerable<EditorDescriptor> specialized = _fileEditorsByExtension.TryGetValue(normalizedExtension, out var descriptors)
                ? descriptors
                : Enumerable.Empty<EditorDescriptor>();
            IEnumerable<EditorDescriptor> all = OrderDescriptors(specialized).Concat(OrderDescriptors(_genericFileEditors));
            if (visibleOnly)
                all = all.Where(descriptor => descriptor.IsVisibleInOpenWith);
            return all.DistinctBy(descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public IReadOnlyList<EditorDescriptor> GetFolderEditorDescriptors(bool visibleOnly = false)
        {
            IEnumerable<EditorDescriptor> descriptors = OrderDescriptors(_folderEditors);
            if (visibleOnly)
                descriptors = descriptors.Where(descriptor => descriptor.IsVisibleInOpenWith);
            return descriptors.ToList();
        }

        public EditorDescriptor? GetDefaultFileEditorDescriptor(string? extension)
        {
            LoadConfiguredEditors();
            string normalizedExtension = NormalizeExtension(extension);
            _fileEditorsByExtension.TryGetValue(normalizedExtension, out var specialized);
            string? configuredId = _configuredFileEditors.TryGetValue(normalizedExtension, out var configured) ? configured.Id : null;
            return SelectDefaultFileEditor(specialized ?? Enumerable.Empty<EditorDescriptor>(), _genericFileEditors, configuredId);
        }

        public EditorDescriptor? GetDefaultFolderEditorDescriptor()
        {
            LoadConfiguredEditors();
            var descriptors = GetFolderEditorDescriptors();
            return FindStoredDescriptor(descriptors, _configuredFolderEditor?.Id)
                ?? descriptors.FirstOrDefault(descriptor => descriptor.IsDefault)
                ?? (descriptors.Count > 0 ? descriptors[0] : null);
        }

        public EditorDescriptor? GetEditorDescriptor(Type editorType, EditorResourceKind resourceKind, string? extension = null)
        {
            IEnumerable<EditorDescriptor> descriptors = resourceKind == EditorResourceKind.Folder
                ? GetFolderEditorDescriptors()
                : GetFileEditorDescriptors(extension);
            return descriptors.FirstOrDefault(descriptor => descriptor.EditorType == editorType);
        }

        public List<Type> GetEditorsForExt(string extension) => GetFileEditorDescriptors(extension).Select(descriptor => descriptor.EditorType).ToList();

        public List<Type> GetVisibleEditorsForExt(string extension) => GetFileEditorDescriptors(extension, visibleOnly: true).Select(descriptor => descriptor.EditorType).ToList();

        public Type? GetDefaultEditorType(string extension) => GetDefaultFileEditorDescriptor(extension)?.EditorType;

        public void SetDefaultEditor(string extension, Type editorType)
        {
            string normalizedExtension = NormalizeExtension(extension);
            var descriptor = GetFileEditorDescriptors(normalizedExtension).FirstOrDefault(item => item.EditorType == editorType);
            if (descriptor == null)
                return;

            _configuredFileEditors[normalizedExtension] = descriptor;
            EditorManagerConfig.Instance.DefaultEditors[normalizedExtension] = descriptor.Id;
            ConfigService.Instance.Save<EditorManagerConfig>();
        }

        public List<Type> GetFolderEditors() => GetFolderEditorDescriptors().Select(descriptor => descriptor.EditorType).ToList();

        public List<Type> GetVisibleFolderEditors() => GetFolderEditorDescriptors(visibleOnly: true).Select(descriptor => descriptor.EditorType).ToList();

        public Type? GetDefaultFolderEditorType() => GetDefaultFolderEditorDescriptor()?.EditorType;

        public void SetDefaultFolderEditor(Type editorType)
        {
            var descriptor = GetFolderEditorDescriptors().FirstOrDefault(item => item.EditorType == editorType);
            if (descriptor == null)
                return;

            _configuredFolderEditor = descriptor;
            EditorManagerConfig.Instance.DefaultFolderEditor = descriptor.Id;
            ConfigService.Instance.Save<EditorManagerConfig>();
        }

        public IEditor? OpenFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;
            return CreateEditor(GetDefaultFileEditorDescriptor(Path.GetExtension(filePath)));
        }

        public bool TryOpenFile(string filePath)
        {
            if (!File.Exists(filePath))
                return false;
            return OpenWithDescriptor(filePath, GetDefaultFileEditorDescriptor(Path.GetExtension(filePath)));
        }

        public bool OpenFileWith(string filePath, Type editorType)
        {
            if (!File.Exists(filePath))
                return false;

            var descriptor = GetFileEditorDescriptors(Path.GetExtension(filePath)).FirstOrDefault(item => item.EditorType == editorType);
            return OpenWithDescriptor(filePath, descriptor);
        }

        public bool OpenFileWith(string filePath, string editorId)
        {
            if (!File.Exists(filePath))
                return false;

            var descriptor = GetFileEditorDescriptors(Path.GetExtension(filePath))
                .FirstOrDefault(item => string.Equals(item.Id, editorId, StringComparison.OrdinalIgnoreCase));
            return OpenWithDescriptor(filePath, descriptor);
        }

        public IEditor? OpenFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return null;
            return CreateEditor(GetDefaultFolderEditorDescriptor());
        }

        public bool TryOpenFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return false;
            return OpenWithDescriptor(folderPath, GetDefaultFolderEditorDescriptor());
        }

        public bool OpenFolderWith(string folderPath, Type editorType)
        {
            if (!Directory.Exists(folderPath))
                return false;

            var descriptor = GetFolderEditorDescriptors().FirstOrDefault(item => item.EditorType == editorType);
            return OpenWithDescriptor(folderPath, descriptor);
        }

        public bool OpenFolderWith(string folderPath, string editorId)
        {
            if (!Directory.Exists(folderPath))
                return false;

            var descriptor = GetFolderEditorDescriptors()
                .FirstOrDefault(item => string.Equals(item.Id, editorId, StringComparison.OrdinalIgnoreCase));
            return OpenWithDescriptor(folderPath, descriptor);
        }

        private static IEditor? CreateEditor(EditorDescriptor? descriptor)
        {
            return descriptor == null ? null : Activator.CreateInstance(descriptor.EditorType) as IEditor;
        }

        private static bool OpenWithDescriptor(string path, EditorDescriptor? descriptor)
        {
            if (CreateEditor(descriptor) is not { } editor)
                return false;
            editor.Open(path);
            return true;
        }
    }
}
