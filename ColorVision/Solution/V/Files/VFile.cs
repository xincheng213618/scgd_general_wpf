using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using System.Windows.Controls;

namespace ColorVision.Solution.V.Files
{
    public class VFile : VObject
    {
        public IFile File { get; set; }

        public VFile(IFile file)
        {
            File = file;
            Name = file.Name;
            ToolTip = file.ToolTip;
            Icon = file.Icon;

            AttributesCommand = new RelayCommand(a => FileProperties.ShowFileProperties(File.FullName), a => true);

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resource.Open, Command = OpenCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resource.Delete, Command = DeleteCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resource.Property, Command = AttributesCommand });
            if (file.ContextMenu != null)
            {
                foreach (MenuItem item in file.ContextMenu.Items)
                {
                    ContextMenu.Items.Add(item);
                }
            }
        }

        public override void Open()
        {
            if (this is VFile vFile)
            {
                if (vFile.File is IFile file)
                {
                    file.Open();
                }
            }
        }

        public override void Copy()
        {
            if (this is VFile vFile)
            {
                if (vFile.File is IFile file)
                {
                    file.Copy();
                }
            }
        }

        public override void ReName()
        {
            if (this is VFile vFile)
            {
                if (vFile.File is IFile file)
                {
                    file.ReName();
                }
            }
        }

        public override void Delete()
        {
            if (this is VFile vFile)
            {
                if (vFile.File is IFile file)
                {
                    file.Delete();
                }
            }
            Parent.RemoveChild(this);
        }

        public override bool CanReName { get => _CanReName; set { _CanReName = value; NotifyPropertyChanged(); } }
        private bool _CanReName = true;

        public override bool CanDelete { get => _CanDelete; set { _CanDelete = value; NotifyPropertyChanged(); } }
        private bool _CanDelete = true;

        public override bool CanAdd { get => _CanAdd; set { _CanAdd = value; NotifyPropertyChanged(); } }
        private bool _CanAdd = true;

        public override bool CanCopy { get => _CanCopy; set { _CanCopy = value; NotifyPropertyChanged(); } }
        private bool _CanCopy = true;

        public override bool CanPaste { get => _CanPaste; set { _CanPaste = value; NotifyPropertyChanged(); } }
        private bool _CanPaste = true;

        public override bool CanCut { get => _CanCut; set { _CanCut = value; NotifyPropertyChanged(); } }
        private bool _CanCut = true;
    }
}
