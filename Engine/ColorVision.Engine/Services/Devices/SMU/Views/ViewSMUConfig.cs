using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ColorVision.Engine.Utilities;
using ColorVision.Engine.Properties;

namespace ColorVision.Engine.Services.Devices.SMU.Views
{
    public class ViewSMUConfig : ViewConfigBase, IConfig
    {
        public static ViewSMUConfig Instance => ConfigService.Instance.GetRequiredService<ViewSMUConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        [LocalizedDisplayName(nameof(Resources.ShowList)), Category("View")]
        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; OnPropertyChanged(); } }
        private bool _IsShowListView = true;
        [LocalizedDisplayName(nameof(Resources.ListHeight)), Category("View")]
        public double Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private double _Height = 200;
    }
}
