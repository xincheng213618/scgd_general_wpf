using ColorVision.Solution.Editor;
using ColorVision.UI;
using System.IO;

namespace ColorVision.Solution
{
    /// <summary>
    /// Configuration class for editor manager settings.
    /// Stores default editor mappings for different file extensions.
    /// </summary>
    public class EditorManagerConfig : IConfig
    {
        public static EditorManagerConfig Instance => ConfigService.Instance.GetRequiredService<EditorManagerConfig>();

        /// <summary>
        /// Dictionary mapping file extensions to default editor type names
        /// Key: file extension (e.g., ".cs"), Value: editor type full name
        /// </summary>
        public Dictionary<string, string> DefaultEditors { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Save the current configuration to persistent storage
        /// </summary>
        public void Save()
        {
            ConfigService.Instance.SaveConfigs();
        }

        /// <summary>
        /// Load configuration from persistent storage
        /// </summary>
        public void Load()
        {
            // Configuration is automatically loaded by ConfigService
        }
    }

    /// <summary>
    /// Manages editor registration and selection for different file types.
    /// Supports automatic registration using attributes and provides methods to
    /// get appropriate editors for files and folders.
    /// </summary>
    public class EditorManager
    {
        public static EditorManager Instance { get; } = new EditorManager();

        // Key: 扩展名（如 .cs），Value: 支持该扩展名的所有编辑器类型
        private readonly Dictionary<string, List<Type>> _editorMappings = new();
        // Key: 扩展名，Value: 默认编辑器类型
        private readonly Dictionary<string, Type> _defaultEditors = new();
        // 支持多个通用编辑器
        private readonly List<Type> _genericEditorTypes = new();

        private const string GENERIC_KEY = "*";

        private EditorManager()
        {
            RegisterEditors();
            LoadDefaultEditors();
        }

        private void LoadDefaultEditors()
        {
            var dict = EditorManagerConfig.Instance.DefaultEditors;
            if (dict != null)
            {
                foreach (var kv in dict)
                {
                    var extLower = kv.Key.ToLowerInvariant();
                    var editorType = AssemblyService.Instance.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.FullName == kv.Value);
                    if (editorType != null)
                        _defaultEditors[extLower] = editorType;
                }
            }
        }

        private void RegisterEditors()
        {
            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IEditor).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        var attr = type.GetCustomAttributes(typeof(EditorForExtensionAttribute), false)
                            .Cast<EditorForExtensionAttribute>().FirstOrDefault();
                        if (attr != null)
                        {
                            foreach (var ext in attr.Extensions)
                            {
                                var extLower = ext.ToLowerInvariant();
                                if (!_editorMappings.ContainsKey(extLower))
                                    _editorMappings[extLower] = new List<Type>();
                                _editorMappings[extLower].Add(type);

                                if (attr.IsDefault && !_defaultEditors.ContainsKey(extLower))
                                    _defaultEditors[extLower] = type;
                            }
                        }
                        // 支持多个通用编辑器
                        if (type.GetCustomAttributes(typeof(GenericEditorAttribute), false).Any())
                        {
                            _genericEditorTypes.Add(type);
                            if (!_editorMappings.ContainsKey(GENERIC_KEY))
                                _editorMappings[GENERIC_KEY] = new List<Type>();
                            _editorMappings[GENERIC_KEY].Add(type);
                        }
                    }
                }
            }
        }
        public static string GetEditorName(Type type)
        {
            var attr = type.GetCustomAttributes(typeof(EditorForExtensionAttribute), false)
                .Cast<EditorForExtensionAttribute>().FirstOrDefault();
            if (attr != null && !string.IsNullOrWhiteSpace(attr.Name))
                return attr.Name!;
            var genericAttr = type.GetCustomAttributes(typeof(GenericEditorAttribute), false)
                .Cast<GenericEditorAttribute>().FirstOrDefault();
            if (genericAttr != null && !string.IsNullOrWhiteSpace(genericAttr.Name))
                return genericAttr.Name!;
            return type.Name;
        }

        public List<Type> GetEditorsForExt(string extension)
        {
            var extLower = extension.ToLowerInvariant();
            var result = new List<Type>();
            if (_editorMappings.TryGetValue(extLower, out var editors))
                result.AddRange(editors);

            // 补充通用编辑器
            if (_editorMappings.TryGetValue(GENERIC_KEY, out var genericEditors))
                result.AddRange(genericEditors);

            return result;
        }

        public Type? GetDefaultEditorType(string extension)
        {
            var extLower = extension.ToLowerInvariant();
            if (_defaultEditors.TryGetValue(extLower, out var t))
                return t;
            // 如果没专用的，取第一个通用编辑器
            if (_editorMappings.TryGetValue(GENERIC_KEY, out var genericEditors) && genericEditors.Count > 0)
                return genericEditors[0];
            return null;
        }

        // 切换默认editor
        public void SetDefaultEditor(string extension, Type editorType)
        {
            var extLower = extension.ToLowerInvariant();
            if (GetEditorsForExt(extLower).Contains(editorType))
            {
                _defaultEditors[extLower] = editorType;
                EditorManagerConfig.Instance.DefaultEditors[extLower] = editorType.FullName;
                EditorManagerConfig.Instance.Save(); // 自动保存配置
            }
        }

        public IEditor? OpenFolder  (string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                var defaultType = typeof(WebView2Editor);
                if (defaultType != null)
                    return Activator.CreateInstance(defaultType) as IEditor;
            }
            return null;
        }

        // 打开文件，返回编辑器实例或提示
        public IEditor? OpenFile(string filePath)
        {

            string extension = Path.GetExtension(filePath);

            // 1. 默认编辑器
            var defaultType = GetDefaultEditorType(extension);
            if (defaultType != null)
                return Activator.CreateInstance(defaultType) as IEditor;
            // 2. 所有可选编辑器（包含通用编辑器）
            var allTypes = GetEditorsForExt(extension);
            if (allTypes.Count > 0)
                return Activator.CreateInstance(allTypes[0]) as IEditor; // 这里可弹窗让用户选

            // 3. 无编辑器
            return null;
        }
    }
}