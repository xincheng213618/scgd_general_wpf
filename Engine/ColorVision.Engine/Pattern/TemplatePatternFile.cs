using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Pattern
{
    public class TemplatePatternFile
    {
        public ContextMenu ContextMenu { get; set; }
        public RelayCommand SelectCommand { get; set; }
        public TemplatePatternFile(string filePath)
        {
            FilePath = filePath;
            Name = Path.GetFileNameWithoutExtension(filePath);
            SelectCommand = new RelayCommand(a => PlatformHelper.OpenFolderAndSelectFile(FilePath));

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = "复制", Command = ApplicationCommands.Copy });
            ContextMenu.Items.Add(new MenuItem() { Header = "删除", Command = ApplicationCommands.Delete });
            ContextMenu.Items.Add(new MenuItem() { Header = "选中", Command = SelectCommand });

        }
        public string Name { get; set; }

        public string FilePath { get; set; }
    }
}
