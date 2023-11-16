using System.Windows.Controls;

namespace ColorVision.Solution.V.Files
{
    public class VFile : VObject
    {
        public IFile File { get; set; }

        public ContextMenu ContextMenu { get; set; }
        public VFile(IFile file)
        {
            File = file;
            Name = file.Name;
            ToolTip = file.ToolTip;
            Icon = file.Icon;
            ContextMenu = file.ContextMenu;
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
