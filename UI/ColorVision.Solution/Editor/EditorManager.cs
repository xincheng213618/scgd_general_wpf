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
        private readonly Dictionary<string, string> _configuredFileEditorIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Assembly> _registeredAssemblies = new();
        private readonly object _syncRoot = new();
        private string? _configuredFolderEditorId;
        private bool _configurationLoaded;

        public event EventHandler? EditorsChanged;

        private EditorManager()
        {
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
            RegisterEditors(AssemblyService.Instance?.GetAssemblies()
                ?? AppDomain.CurrentDomain.GetAssemblies());
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

        private void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs e)
        {
            RegisterEditors([e.LoadedAssembly]);
        }

        private void RegisterEditors(IEnumerable<Assembly> assemblies)
        {
            bool changed = false;
            lock (_syncRoot)
            {
                foreach (Assembly assembly in assemblies)
                {
                    if (!_registeredAssemblies.Add(assembly))
                        continue;

                    foreach (Type type in GetLoadableTypes(assembly))
                    {
                        if (!typeof(IEditor).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract)
                            continue;

                        if (type.GetCustomAttribute<EditorForExtensionAttribute>() is { } extensionAttribute)
                        {
                            changed |= TryRegisterDiscoveredEditor(new EditorDescriptor(
                                GetEditorId(type, extensionAttribute.EditorId),
                                type,
                                EditorResourceKind.File,
                                extensionAttribute.Extensions,
                                IsGeneric: false,
                                extensionAttribute.IsDefault,
                                extensionAttribute.Priority,
                                extensionAttribute.IsVisibleInOpenWith,
                                GetLocalizedName(extensionAttribute.ResourceKey, extensionAttribute.Name)));
                        }

                        if (type.GetCustomAttribute<GenericEditorAttribute>() is { } genericAttribute)
                        {
                            changed |= TryRegisterDiscoveredEditor(new EditorDescriptor(
                                GetEditorId(type, genericAttribute.EditorId),
                                type,
                                EditorResourceKind.File,
                                Array.Empty<string>(),
                                IsGeneric: true,
                                genericAttribute.IsDefault,
                                genericAttribute.Priority,
                                genericAttribute.IsVisibleInOpenWith,
                                GetLocalizedName(genericAttribute.ResourceKey, genericAttribute.Name)));
                        }

                        if (type.GetCustomAttribute<FolderEditorAttribute>() is { } folderAttribute)
                        {
                            changed |= TryRegisterDiscoveredEditor(new EditorDescriptor(
                                GetEditorId(type, folderAttribute.EditorId),
                                type,
                                EditorResourceKind.Folder,
                                Array.Empty<string>(),
                                IsGeneric: false,
                                folderAttribute.IsDefault,
                                folderAttribute.Priority,
                                folderAttribute.IsVisibleInOpenWith,
                                GetLocalizedName(folderAttribute.ResourceKey, folderAttribute.Name)));
                        }
                    }
                }
            }

            if (changed)
                EditorsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RegisterEditor(EditorDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            bool changed;
            lock (_syncRoot)
                changed = RegisterEditorCore(descriptor);
            if (changed)
                EditorsChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool TryRegisterDiscoveredEditor(EditorDescriptor descriptor)
        {
            try
            {
                return RegisterEditorCore(descriptor);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                return false;
            }
        }

        private bool RegisterEditorCore(EditorDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor.Id))
                throw new ArgumentException("编辑器 Id 不允许为空。", nameof(descriptor));
            if (!typeof(IEditor).IsAssignableFrom(descriptor.EditorType)
                || descriptor.EditorType.IsAbstract
                || descriptor.EditorType.IsInterface)
            {
                throw new ArgumentException("编辑器类型必须是可实例化的 IEditor。", nameof(descriptor));
            }

            string id = descriptor.Id.Trim();
            string[] extensions = (descriptor.Extensions ?? Array.Empty<string>())
                .Select(NormalizeExtension)
                .Where(extension => extension.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (descriptor.ResourceKind == EditorResourceKind.File
                && !descriptor.IsGeneric
                && extensions.Length == 0)
            {
                throw new ArgumentException("非通用文件编辑器必须声明至少一个扩展名。", nameof(descriptor));
            }

            var normalized = descriptor with
            {
                Id = id,
                Extensions = descriptor.ResourceKind == EditorResourceKind.Folder ? Array.Empty<string>() : extensions,
                IsGeneric = descriptor.ResourceKind == EditorResourceKind.File && descriptor.IsGeneric,
                DisplayName = string.IsNullOrWhiteSpace(descriptor.DisplayName) ? null : descriptor.DisplayName.Trim(),
            };

            RemoveEditorById(id);
            if (normalized.ResourceKind == EditorResourceKind.Folder)
            {
                _folderEditors.Add(normalized);
            }
            else if (normalized.IsGeneric)
            {
                _genericFileEditors.Add(normalized);
            }
            else
            {
                foreach (string extension in normalized.Extensions)
                {
                    if (!_fileEditorsByExtension.TryGetValue(extension, out List<EditorDescriptor>? descriptors))
                    {
                        descriptors = new List<EditorDescriptor>();
                        _fileEditorsByExtension[extension] = descriptors;
                    }
                    descriptors.Add(normalized);
                }
            }
            return true;
        }

        private void RemoveEditorById(string editorId)
        {
            _genericFileEditors.RemoveAll(item => string.Equals(item.Id, editorId, StringComparison.OrdinalIgnoreCase));
            _folderEditors.RemoveAll(item => string.Equals(item.Id, editorId, StringComparison.OrdinalIgnoreCase));
            foreach (List<EditorDescriptor> descriptors in _fileEditorsByExtension.Values)
                descriptors.RemoveAll(item => string.Equals(item.Id, editorId, StringComparison.OrdinalIgnoreCase));
        }

        private void LoadConfiguredEditors()
        {
            lock (_syncRoot)
            {
                if (_configurationLoaded || ConfigService.Instance == null)
                    return;

                EditorManagerConfig config = ConfigService.Instance.GetRequiredService<EditorManagerConfig>();
                foreach (var pair in config.DefaultEditors)
                {
                    string extension = NormalizeExtension(pair.Key);
                    if (extension.Length > 0 && !string.IsNullOrWhiteSpace(pair.Value))
                        _configuredFileEditorIds[extension] = pair.Value;
                }

                _configuredFolderEditorId = string.IsNullOrWhiteSpace(config.DefaultFolderEditor)
                    ? null
                    : config.DefaultFolderEditor;
                _configurationLoaded = true;
            }
        }

        private static string GetEditorId(Type type, string? registeredId)
        {
            return string.IsNullOrWhiteSpace(registeredId)
                ? type.FullName ?? type.Name
                : registeredId.Trim();
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

        public static string GetEditorName(EditorDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return string.IsNullOrWhiteSpace(descriptor.DisplayName)
                ? GetEditorName(descriptor.EditorType)
                : descriptor.DisplayName;
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
            List<EditorDescriptor> specialized;
            List<EditorDescriptor> generic;
            lock (_syncRoot)
            {
                specialized = _fileEditorsByExtension.TryGetValue(normalizedExtension, out var descriptors)
                    ? descriptors.ToList()
                    : new List<EditorDescriptor>();
                generic = _genericFileEditors.ToList();
            }

            IEnumerable<EditorDescriptor> all = OrderDescriptors(specialized).Concat(OrderDescriptors(generic));
            if (visibleOnly)
                all = all.Where(descriptor => descriptor.IsVisibleInOpenWith);
            return all.DistinctBy(descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public IReadOnlyList<EditorDescriptor> GetFolderEditorDescriptors(bool visibleOnly = false)
        {
            List<EditorDescriptor> snapshot;
            lock (_syncRoot)
                snapshot = _folderEditors.ToList();
            IEnumerable<EditorDescriptor> descriptors = OrderDescriptors(snapshot);
            if (visibleOnly)
                descriptors = descriptors.Where(descriptor => descriptor.IsVisibleInOpenWith);
            return descriptors.ToList();
        }

        public EditorDescriptor? GetDefaultFileEditorDescriptor(string? extension)
        {
            LoadConfiguredEditors();
            string normalizedExtension = NormalizeExtension(extension);
            List<EditorDescriptor> specialized;
            List<EditorDescriptor> generic;
            string? configuredId;
            lock (_syncRoot)
            {
                specialized = _fileEditorsByExtension.TryGetValue(normalizedExtension, out var descriptors)
                    ? descriptors.ToList()
                    : new List<EditorDescriptor>();
                generic = _genericFileEditors.ToList();
                configuredId = _configuredFileEditorIds.GetValueOrDefault(normalizedExtension);
            }
            return SelectDefaultFileEditor(specialized, generic, configuredId);
        }

        public EditorDescriptor? GetDefaultFolderEditorDescriptor()
        {
            LoadConfiguredEditors();
            var descriptors = GetFolderEditorDescriptors();
            string? configuredId;
            lock (_syncRoot)
                configuredId = _configuredFolderEditorId;
            return FindStoredDescriptor(descriptors, configuredId)
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

            SetDefaultEditor(normalizedExtension, descriptor.Id);
        }

        public bool SetDefaultEditor(string extension, string editorId)
        {
            return TrySetDefaultEditor(extension, editorId, out _);
        }

        public bool TrySetDefaultEditor(string extension, string editorId, out string errorMessage)
        {
            string normalizedExtension = NormalizeExtension(extension);
            var descriptor = GetFileEditorDescriptors(normalizedExtension).FirstOrDefault(item =>
                string.Equals(item.Id, editorId, StringComparison.OrdinalIgnoreCase));
            if (descriptor == null)
            {
                errorMessage = $"编辑器“{editorId}”不支持 {normalizedExtension} 文件。";
                return false;
            }

            try
            {
                EditorManagerConfig config = EditorManagerConfig.Instance;
                lock (_syncRoot)
                {
                    bool hadPreviousValue = config.DefaultEditors.TryGetValue(normalizedExtension, out string? previousValue);
                    config.DefaultEditors[normalizedExtension] = descriptor.Id;
                    try
                    {
                        ConfigService.Instance.Save<EditorManagerConfig>();
                    }
                    catch
                    {
                        if (hadPreviousValue)
                            config.DefaultEditors[normalizedExtension] = previousValue!;
                        else
                            config.DefaultEditors.Remove(normalizedExtension);
                        throw;
                    }
                    _configuredFileEditorIds[normalizedExtension] = descriptor.Id;
                }
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"保存默认编辑器失败：{ex.Message}";
                return false;
            }
        }

        public List<Type> GetFolderEditors() => GetFolderEditorDescriptors().Select(descriptor => descriptor.EditorType).ToList();

        public List<Type> GetVisibleFolderEditors() => GetFolderEditorDescriptors(visibleOnly: true).Select(descriptor => descriptor.EditorType).ToList();

        public Type? GetDefaultFolderEditorType() => GetDefaultFolderEditorDescriptor()?.EditorType;

        public void SetDefaultFolderEditor(Type editorType)
        {
            var descriptor = GetFolderEditorDescriptors().FirstOrDefault(item => item.EditorType == editorType);
            if (descriptor == null)
                return;

            SetDefaultFolderEditor(descriptor.Id);
        }

        public bool SetDefaultFolderEditor(string editorId)
        {
            return TrySetDefaultFolderEditor(editorId, out _);
        }

        public bool TrySetDefaultFolderEditor(string editorId, out string errorMessage)
        {
            var descriptor = GetFolderEditorDescriptors().FirstOrDefault(item =>
                string.Equals(item.Id, editorId, StringComparison.OrdinalIgnoreCase));
            if (descriptor == null)
            {
                errorMessage = $"编辑器“{editorId}”不支持打开文件夹。";
                return false;
            }

            try
            {
                EditorManagerConfig config = EditorManagerConfig.Instance;
                lock (_syncRoot)
                {
                    string? previousValue = config.DefaultFolderEditor;
                    config.DefaultFolderEditor = descriptor.Id;
                    try
                    {
                        ConfigService.Instance.Save<EditorManagerConfig>();
                    }
                    catch
                    {
                        config.DefaultFolderEditor = previousValue;
                        throw;
                    }
                    _configuredFolderEditorId = descriptor.Id;
                }
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"保存默认文件夹编辑器失败：{ex.Message}";
                return false;
            }
        }

        public IEditor? OpenFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;
            return CreateEditor(GetDefaultFileEditorDescriptor(Path.GetExtension(filePath)));
        }

        public bool TryOpenFile(string filePath)
        {
            return TryOpenFile(filePath, out _);
        }

        public bool TryOpenFile(string filePath, out string errorMessage)
        {
            if (!File.Exists(filePath))
            {
                errorMessage = $"文件不存在：{filePath}";
                return false;
            }
            return OpenWithDescriptor(
                filePath,
                GetDefaultFileEditorDescriptor(Path.GetExtension(filePath)),
                out errorMessage);
        }

        public bool OpenFileWith(string filePath, Type editorType)
        {
            return OpenFileWith(filePath, editorType, out _);
        }

        public bool OpenFileWith(string filePath, Type editorType, out string errorMessage)
        {
            if (!File.Exists(filePath))
            {
                errorMessage = $"文件不存在：{filePath}";
                return false;
            }

            var descriptor = GetFileEditorDescriptors(Path.GetExtension(filePath)).FirstOrDefault(item => item.EditorType == editorType);
            return OpenWithDescriptor(filePath, descriptor, out errorMessage);
        }

        public bool OpenFileWith(string filePath, string editorId)
        {
            return OpenFileWith(filePath, editorId, out _);
        }

        public bool OpenFileWith(string filePath, string editorId, out string errorMessage)
        {
            if (!File.Exists(filePath))
            {
                errorMessage = $"文件不存在：{filePath}";
                return false;
            }

            var descriptor = GetFileEditorDescriptors(Path.GetExtension(filePath))
                .FirstOrDefault(item => string.Equals(item.Id, editorId, StringComparison.OrdinalIgnoreCase));
            return OpenWithDescriptor(filePath, descriptor, out errorMessage);
        }

        public IEditor? OpenFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return null;
            return CreateEditor(GetDefaultFolderEditorDescriptor());
        }

        public bool TryOpenFolder(string folderPath)
        {
            return TryOpenFolder(folderPath, out _);
        }

        public bool TryOpenFolder(string folderPath, out string errorMessage)
        {
            if (!Directory.Exists(folderPath))
            {
                errorMessage = $"文件夹不存在：{folderPath}";
                return false;
            }
            return OpenWithDescriptor(folderPath, GetDefaultFolderEditorDescriptor(), out errorMessage);
        }

        public bool OpenFolderWith(string folderPath, Type editorType)
        {
            return OpenFolderWith(folderPath, editorType, out _);
        }

        public bool OpenFolderWith(string folderPath, Type editorType, out string errorMessage)
        {
            if (!Directory.Exists(folderPath))
            {
                errorMessage = $"文件夹不存在：{folderPath}";
                return false;
            }

            var descriptor = GetFolderEditorDescriptors().FirstOrDefault(item => item.EditorType == editorType);
            return OpenWithDescriptor(folderPath, descriptor, out errorMessage);
        }

        public bool OpenFolderWith(string folderPath, string editorId)
        {
            return OpenFolderWith(folderPath, editorId, out _);
        }

        public bool OpenFolderWith(string folderPath, string editorId, out string errorMessage)
        {
            if (!Directory.Exists(folderPath))
            {
                errorMessage = $"文件夹不存在：{folderPath}";
                return false;
            }

            var descriptor = GetFolderEditorDescriptors()
                .FirstOrDefault(item => string.Equals(item.Id, editorId, StringComparison.OrdinalIgnoreCase));
            return OpenWithDescriptor(folderPath, descriptor, out errorMessage);
        }

        private static IEditor? CreateEditor(EditorDescriptor? descriptor)
        {
            return descriptor == null ? null : Activator.CreateInstance(descriptor.EditorType) as IEditor;
        }

        private static bool OpenWithDescriptor(
            string path,
            EditorDescriptor? descriptor,
            out string errorMessage)
        {
            if (descriptor == null)
            {
                errorMessage = "没有找到可用于打开此资源的编辑器。";
                return false;
            }

            IEditor? editor = null;
            try
            {
                editor = CreateEditor(descriptor);
                if (editor == null)
                {
                    errorMessage = $"无法创建编辑器“{GetEditorName(descriptor)}”。";
                    return false;
                }
                editor.Open(path);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                if (editor is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch
                    {
                    }
                }

                Exception failure = ex is TargetInvocationException { InnerException: { } innerException }
                    ? innerException
                    : ex;
                string resourceName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
                errorMessage = $"编辑器“{GetEditorName(descriptor)}”无法打开“{resourceName}”：{failure.Message}";
                return false;
            }
        }
    }
}
