using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Camera.Views
{
    public class ViewCameraConfig : ViewModelBase, IConfig
    {
        public static ViewCameraConfig Instance => ConfigHandler.GetInstance().GetRequiredService<ViewCameraConfig>();


        public RelayCommand EditCommand { get; set; }

        public ViewCameraConfig()
        {
            EditCommand = new RelayCommand(a => new EditConfig().ShowDialog());
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        public ImageViewConfig ImageViewConfig { get; set; } = new ImageViewConfig();

        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; NotifyPropertyChanged(); } }
        private bool _IsShowListView = true;

        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView;


    }
}
