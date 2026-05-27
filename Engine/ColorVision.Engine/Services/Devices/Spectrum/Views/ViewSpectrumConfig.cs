
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views
{
    public class ViewSpectrumConfig : ViewConfigBase, IConfig
    {
        public static ViewSpectrumConfig Instance => ConfigService.Instance.GetRequiredService<ViewSpectrumConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
        
        [ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "ShowList"), Category("View")]
        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; OnPropertyChanged(); } }
        private bool _IsShowListView = true;
        [ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "ListHeight"), Category("View")]
        public double Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private double _Height = 200;

        [ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "ShowSidebar"), Category("Control")]
        public bool IsShowSideListView { get => _IsShowSideListView; set { _IsShowSideListView = value; OnPropertyChanged(); } }
        private bool _IsShowSideListView;

        [Display(Name = "Engine_PG_NegativeLuminanceGuard", GroupName = "Spectrum", ResourceType = typeof(Properties.Resources))]
        public bool EnableNegativeLuminanceGuard { get => _EnableNegativeLuminanceGuard; set { _EnableNegativeLuminanceGuard = value; OnPropertyChanged(); } }
        private bool _EnableNegativeLuminanceGuard = true;

        [Display(Name = "Engine_PG_MinLuminanceValue", GroupName = "Spectrum", ResourceType = typeof(Properties.Resources))]
        public double MinLuminanceValue { get => _MinLuminanceValue; set { _MinLuminanceValue = value; OnPropertyChanged(); } }
        private double _MinLuminanceValue = 0.0001;
    }
}
