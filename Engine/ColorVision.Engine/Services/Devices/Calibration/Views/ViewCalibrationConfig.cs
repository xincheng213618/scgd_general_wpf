using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Calibration.Views
{
    [ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "CalibrationViewSettings")]
    public class ViewCalibrationConfig : ViewConfigBase, IConfig
    {
        public static ViewCalibrationConfig Instance => ConfigService.Instance.GetRequiredService<ViewCalibrationConfig>();
        [JsonIgnore]
        public ObservableCollection<ViewResultImage> ViewResults { get; set; } = new ObservableCollection<ViewResultImage>();
        [JsonIgnore]
        public RelayCommand ClearListCommand { get; set; }
        public ViewCalibrationConfig()
        {
            ClearListCommand = new RelayCommand(a => ViewResults.Clear());
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();


        [ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "ShowList"), Category("View")]
        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; OnPropertyChanged(); } }
        private bool _IsShowListView = true;
        [ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "ListHeight"), Category("View")]
        public double Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private double _Height = 200;



        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; OnPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;

    }
}
  