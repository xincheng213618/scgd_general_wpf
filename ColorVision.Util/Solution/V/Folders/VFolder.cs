using System.Windows.Controls;
using System.Windows.Media;
using ColorVision.Util.Properties;

namespace ColorVision.Solution.V.Folders
{
    public class VFolder : VObject
    {
        public IFolder Folder { get; set; }

        public VFolder(IFolder folder)
        {
            Folder = folder;
            Name = folder.Name;
            ToolTip = folder.ToolTip;

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Open, Command = OpenCommand });
        }

        public override ImageSource Icon {get => Folder.Icon; set { Folder.Icon = value; NotifyPropertyChanged(); } }

        public override void Open()
        {
            if (this is VFolder vFolder)
            {
                if (vFolder.Folder is IFolder folder)
                {
                    folder.Open();
                }
            }
        }

        public override void Copy()
        {
            if (this is VFolder vFolder)
            {
                if (vFolder.Folder is IFolder folder)
                {
                    folder.Copy();
                }
            }
        }

        public override void ReName()
        {
            if (this is VFolder vFolder)
            {
                if (vFolder.Folder is IFolder folder)
                {
                    folder.ReName();
                }
            }
        }

        public override void Delete()
        {
            if (this is VFolder vFolder)
            {
                if (vFolder.Folder is IFolder folder)
                {
                    folder.Delete();
                }
            }
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
