using ColorVision.UI.Menus;
using ColorVision.UI.Properties;
using System.IO;
using System.Windows;

namespace ColorVision.UI
{
    public class MenuOpen : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.File;

        public override string GuidId => nameof(MenuOpen);

        public override int Order => 1;

        public override string Header => Resources.MenuOpen;
    }

    public class MenuFileOpen : MenuItemBase
    {

        public override int Order => 1;

        public override string Header => "文件(_F)...";

        public override string OwnerGuid => "MenuOpen";

        public override string GuidId => nameof(MenuFileOpen);

        public override void Execute()
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = FileProcessorFactory.GetInstance().GetCombinedExtensions() + "|所有文件 (*.*)|*.*";
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
                    MessageBox.Show("不支持的文件格式");
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

        private readonly List<IFileProcessor> _fileProcessors;

        private FileProcessorFactory()
        {
            _fileProcessors = new List<IFileProcessor>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IFileProcessor).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IFileProcessor fileHandler)
                        {
                            _fileProcessors.Add(fileHandler);
                        }
                    }
                    catch
                    {
                        // Log or handle the exception
                    }
                }
            }
            _fileProcessors = _fileProcessors.OrderBy(handler => handler.Order).ToList();
        }

        public string GetCombinedExtensions()
        {
            var allExtensions = _fileProcessors
                .Select(processor => processor.GetExtension())
                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                .Distinct();

            return string.Join("|", allExtensions);
        }

        public IFileProcessor? GetFileProcessor(string filePath)
        {
            return _fileProcessors.FirstOrDefault(processor => processor.CanProcess(filePath));
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
            foreach (var processor in _fileProcessors)
            {
                if (processor.CanExport(filePath))
                {
                    processor.Export(filePath);
                    return true;
                }
            }
            return false;
        }
    }
}
