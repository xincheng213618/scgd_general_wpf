using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.UI.PropertyEditor;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Views
{
    public class ViewCameraConfig : ViewConfigBase, IConfig
    {
        public static ViewCameraConfig Instance => ConfigService.Instance.GetRequiredService<ViewCameraConfig>();

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
