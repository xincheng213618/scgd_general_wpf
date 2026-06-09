#pragma warning disable CA1805
using ColorVision.Common.MVVM;
using ColorVision.UI;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    public class ModelViewer3DConfig : ViewModelBase, IConfig
    {
        public static ModelViewer3DConfig Instance => ConfigService.Instance.GetRequiredService<ModelViewer3DConfig>();

        public string LastOpenDirectory
        {
            get => _LastOpenDirectory;
            set { _LastOpenDirectory = value; OnPropertyChanged(); }
        }
        private string _LastOpenDirectory = string.Empty;

        public bool DefaultWireframe
        {
            get => _DefaultWireframe;
            set { _DefaultWireframe = value; OnPropertyChanged(); }
        }
        private bool _DefaultWireframe = false;

        public double FieldOfView
        {
            get => _FieldOfView;
            set { _FieldOfView = value; OnPropertyChanged(); }
        }
        private double _FieldOfView = 60;

        public bool IsToolbarVisible
        {
            get => _IsToolbarVisible;
            set { _IsToolbarVisible = value; OnPropertyChanged(); }
        }
        private bool _IsToolbarVisible = true;

        public bool IsTextureVisible
        {
            get => _IsTextureVisible;
            set { _IsTextureVisible = value; OnPropertyChanged(); }
        }
        private bool _IsTextureVisible = true;

        public bool IsMaterialVisible
        {
            get => _IsMaterialVisible;
            set { _IsMaterialVisible = value; OnPropertyChanged(); }
        }
        private bool _IsMaterialVisible = true;

        public bool HideExportedTextureFiles
        {
            get => _HideExportedTextureFiles;
            set { _HideExportedTextureFiles = value; OnPropertyChanged(); }
        }
        private bool _HideExportedTextureFiles = true;
    }
}
