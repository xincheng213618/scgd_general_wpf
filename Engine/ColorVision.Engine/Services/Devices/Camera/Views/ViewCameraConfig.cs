using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ColorVision.Engine.Utilities;
using ColorVision.Engine.Properties;

namespace ColorVision.Engine.Services.Devices.Camera.Views
{
    [LocalizedDisplayName(nameof(Resources.CameraViewConfig))]
    public class ViewCameraConfig : ViewConfigBase, IConfig
    {
        public static ViewCameraConfig Instance => ConfigService.Instance.GetRequiredService<ViewCameraConfig>();

        [JsonIgnore]
        public ObservableCollection<ViewResultImage> ViewResults { get; set; } = new ObservableCollection<ViewResultImage>();

        [JsonIgnore]
        public RelayCommand ClearListCommand { get; set; }
        public ViewCameraConfig()
        {
            ClearListCommand = new RelayCommand(a => ViewResults.Clear());
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();


        [LocalizedDisplayName(nameof(Resources.ShowList)), Category("View")]
        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; OnPropertyChanged(); } }
        private bool _IsShowListView = true;

        [LocalizedDisplayName(nameof(Resources.ListHeight)), Category("View")]
        public double Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private double _Height = 200;


        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; OnPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;



    }
}
