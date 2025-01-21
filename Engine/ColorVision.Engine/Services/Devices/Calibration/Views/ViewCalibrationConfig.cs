using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Dao;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ColorVision.Engine.Services.Devices.Calibration.Views
{
    [DisplayName("校正视图配置")]
    public class ViewCalibrationConfig : ViewConfigBase, IConfig
    {
        public static ViewCalibrationConfig Instance => ConfigService.Instance.GetRequiredService<ViewCalibrationConfig>();
        [JsonIgnore]
        public ObservableCollection<ViewResultCamera> ViewResults { get; set; } = new ObservableCollection<ViewResultCamera>();
        [JsonIgnore]
        public RelayCommand ClearListCommand { get; set; }
        public ViewCalibrationConfig()
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
