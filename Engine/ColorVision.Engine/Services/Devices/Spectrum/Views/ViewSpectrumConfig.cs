#pragma warning disable CS8604

using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views
{
    public class ViewSpectrumConfig : ViewConfigBase, IConfig
    {
        public static ViewSpectrumConfig Instance => ConfigService.Instance.GetRequiredService<ViewSpectrumConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

    }
}
