using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base.File;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI
{
    public class MenuFileOpen : MenuItemBase
    {
        public override int Order => 1;

        public override string Header => Properties.Resources.MenuFile + "...";

        public override string OwnerGuid => nameof(MenuOpen);

        public override string GuidId => nameof(MenuFileOpen);
        public override object? Icon
        {
            get
            {
                TextBlock text = new()
                {
                    Text = "\uE8E5", // 使用Unicode字符
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 15,
                };
                text.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                return text;
            }
        }
        public override void Execute()
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = Properties.Resources.AllFiles;
            openFileDialog.FilterIndex = openFileDialog.Filter.Split('|').Length / 2; ;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedFilePath = openFileDialog.FileName;
                if (string.IsNullOrEmpty(selectedFilePath))
                {
                    return;
                }
                IFileProcessor fileProcessor = FileProcessorFactory.GetInstance().GetFileProcessor(selectedFilePath);
                if (fileProcessor == null)
                {
                    MessageBox.Show(Properties.Resources.UnsupportedFileFormat);
                    return;
                }
                fileProcessor.Process(selectedFilePath);
            }
        }
    }

    public class FileProcessorFactory
    {
        private static readonly Lazy<FileProcessorFactory> _instance = new(() => new FileProcessorFactory());
        public static FileProcessorFactory GetInstance() => _instance.Value;

        private readonly Dictionary<string, List<Type>> _extTypeMap = new();
        private Type? _genericType;

        private FileProcessorFactory()
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IFileProcessor).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
                {
                    // 检查是否是兜底处理器
                    if (type.GetCustomAttribute<GenericFileAttribute>() != null)
                    {
                        _genericType = type;
                        continue;
                    }
                    var extAttr = type.GetCustomAttribute<FileExtensionAttribute>();
                    if (extAttr != null)
                    {
                        foreach (var ext in extAttr.Extensions)
                        {
                            var extKey = ext.ToLowerInvariant();
                            if (!_extTypeMap.ContainsKey(extKey))
                                _extTypeMap[extKey] = new List<Type>();
                            _extTypeMap[extKey].Add(type);
                        }
                    }
                }
            }
        }

        public IFileProcessor? GetFileProcessor(string filePath)
        {
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            List<Type>? typeList = null;
            if (!string.IsNullOrWhiteSpace(ext))
                _extTypeMap.TryGetValue(ext, out typeList);

            var candidates = new List<IFileProcessor>();

            if (typeList != null && typeList.Count > 0)
            {
                foreach (var t in typeList)
                {
                    if (Activator.CreateInstance(t) is IFileProcessor processor && processor.CanProcess(filePath))
                    {
                        candidates.Add(processor);
                    }
                }
            }

            // 如果有候选，取 Order 最大的
            if (candidates.Count > 0)
                return candidates.OrderByDescending(p => p.Order).First();

            // 如果没有，尝试 fallback
            if (_genericType != null)
            {
                if (Activator.CreateInstance(_genericType) is IFileProcessor fallback && fallback.CanProcess(filePath))
                    return fallback;
            }
            return null;
        }

        public bool HandleFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            var processor = GetFileProcessor(filePath);
            if (processor != null)
            {
                processor.Process(filePath);
                return true;
            }
            return false;
        }

        public bool ExportFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            List<Type>? typeList = null;
            if (!string.IsNullOrWhiteSpace(ext))
                _extTypeMap.TryGetValue(ext, out typeList);

            var candidates = new List<IFileProcessor>();

            if (typeList != null && typeList.Count > 0)
            {
                foreach (var t in typeList)
                {
                    if (Activator.CreateInstance(t) is IFileProcessor processor && processor.CanExport(filePath))
                    {
                        candidates.Add(processor);
                    }
                }
            }

            // 先取 Order 最大的能导出的
            if (candidates.Count > 0)
            {
                var best = candidates.OrderByDescending(p => p.Order).First();
                best.Export(filePath);
                return true;
            }

            // fallback
            if (_genericType != null)
            {
                if (Activator.CreateInstance(_genericType) is IFileProcessor fallback && fallback.CanExport(filePath))
                {
                    fallback.Export(filePath);
                    return true;
                }
            }
            return false;
        }
    }
}
