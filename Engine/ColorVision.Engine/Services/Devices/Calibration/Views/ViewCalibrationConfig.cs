using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Calibration.Views
{
    public class ViewCalibrationConfig : ViewModelBase, IConfig
    {
        public static ViewCalibrationConfig Instance => ConfigService.Instance.GetRequiredService<ViewCalibrationConfig>();


        public RelayCommand EditCommand { get; set; }

        public ViewCalibrationConfig()
        {
            EditCommand = new RelayCommand(a => new EditViewCameraConfig(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        public ImageViewConfig ImageViewConfig { get; set; } = new ImageViewConfig();

        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; NotifyPropertyChanged(); } }
        private bool _IsShowListView = true;

        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; NotifyPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;

        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView = true;

        public bool InsertAtBeginning { get => _InsertAtBeginning; set { _InsertAtBeginning = value; NotifyPropertyChanged(); } }
        private bool _InsertAtBeginning = true;

    }
}
