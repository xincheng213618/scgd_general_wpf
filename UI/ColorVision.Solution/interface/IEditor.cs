using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Solution
{
    // 自定义Attribute用于声明扩展名和默认项
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class EditorForExtensionAttribute : Attribute
    {
        public string[] Extensions { get; }
        public bool IsDefault { get; }

        public EditorForExtensionAttribute(string extensions, bool isDefault = false)
        {
            Extensions = extensions.Split('|');
            IsDefault = isDefault;
        }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class GenericEditorAttribute : Attribute { }

    public class EditorManager
    {
        public static EditorManager Instance { get; } = new EditorManager();

        // Key: 扩展名（如 .cs），Value: 支持该扩展名的所有编辑器类型
        private readonly Dictionary<string, List<Type>> _editorMappings = new();
        // Key: 扩展名，Value: 默认编辑器类型
        private readonly Dictionary<string, Type> _defaultEditors = new();
        // 通用编辑器类型
        private Type? _genericEditorType;

        private EditorManager()
        {
            RegisterEditors();
        }

        private void RegisterEditors()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
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
                                if (!_editorMappings.ContainsKey(ext))
                                    _editorMappings[ext] = new List<Type>();
                                _editorMappings[ext].Add(type);

                                // 默认编辑器
                                if (attr.IsDefault)
                                    _defaultEditors[ext] = type;
                            }
                        }
                        else if (type.GetCustomAttributes(typeof(GenericEditorAttribute), false).Any())
                        {
                            _genericEditorType = type;
                        }
                    }
                }
            }
        }

        // 获取所有支持该扩展名的editor类型
        public List<Type> GetEditorsForExt(string extension)
            => _editorMappings.TryGetValue(extension, out var editors) ? editors : new List<Type>();

        // 获取默认editor类型
        public Type? GetDefaultEditorType(string extension)
            => _defaultEditors.TryGetValue(extension, out var t) ? t : null;

        // 切换默认editor
        public void SetDefaultEditor(string extension, Type editorType)
        {
            if (_editorMappings.TryGetValue(extension, out var list) && list.Contains(editorType))
                _defaultEditors[extension] = editorType;
        }

        // 打开文件，返回编辑器实例或提示
        public IEditor? OpenFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);

            // 1. 默认编辑器
            var defaultType = GetDefaultEditorType(extension);
            if (defaultType != null)
                return Activator.CreateInstance(defaultType) as IEditor;

            // 2. 其它可选编辑器
            var allTypes = GetEditorsForExt(extension);
            if (allTypes.Count > 0)
                return Activator.CreateInstance(allTypes[0]) as IEditor; // 这里可弹窗让用户选

            // 3. 通用编辑器
            if (_genericEditorType != null)
                return Activator.CreateInstance(_genericEditorType) as IEditor;

            // 4. 无编辑器
            return null;
        }
    }
    public interface IEditor
    {
        string Name { get; }
        Control? Open(string filePath);
    }

    public abstract class EditorBase : IEditor
    {
        public virtual string Name { get; set; }
        public abstract Control? Open(string filePath);
    }


    // 标记为通用编辑器
    [GenericEditor]
    public class SystemEditor : EditorBase
    {
        public override string Name => "系统默认打开";

        public override Control? Open(string filePath)
        {
            PlatformHelper.Open(filePath);
            return null;
        }
    }
}