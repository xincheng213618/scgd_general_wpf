using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Dao;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Camera.Views
{
    [DisplayName("相机视图配置")]
    public class ViewCameraConfig : ViewConfigBase, IConfig
    {
        public static ViewCameraConfig Instance => ConfigService.Instance.GetRequiredService<ViewCameraConfig>();

        [JsonIgnore]
        public ObservableCollection<ViewResultCamera> ViewResults { get; set; } = new ObservableCollection<ViewResultCamera>();

        [JsonIgnore]
        public RelayCommand ClearListCommand { get; set; }
        public ViewCameraConfig()
        {
            ClearListCommand = new RelayCommand(a => ViewResults.Clear());
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        public ImageViewConfig ImageViewConfig { get; set; } = new ImageViewConfig();

        [DisplayName("显示数据列"), Category("Control")]
        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; NotifyPropertyChanged(); } }
        private bool _IsShowListView = true;

        [DisplayName("打开图像超时")]
        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; NotifyPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;

    }
}
