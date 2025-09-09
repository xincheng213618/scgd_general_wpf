using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pattern
{
    public class TemplatePatternFile:ViewModelBase
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
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public  bool IsEditMode { get => _IsEditMode; set {
                if (_IsEditMode == value) return;
                _IsEditMode = value;
                OnPropertyChanged();
                if (!value)
                {
                    string newpath = Path.Combine(Path.GetDirectoryName(FilePath), Name+ Path.GetExtension(FilePath));
                    if (newpath !=FilePath)
                    File.Move(FilePath, newpath);
                    FilePath = newpath;
                }

            } 
        }
        private bool _IsEditMode;

        public string FilePath { get; set; }
    }
}
