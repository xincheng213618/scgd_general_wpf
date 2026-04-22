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
    }
}
