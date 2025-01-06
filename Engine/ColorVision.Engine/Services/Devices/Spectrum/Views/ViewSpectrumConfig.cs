#pragma warning disable CS8604

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.PropertyEditor;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views
{
    public class ViewSpectrumConfig : ViewConfigBase, IConfig
    {
        public static ViewSpectrumConfig Instance => ConfigService.Instance.GetRequiredService<ViewSpectrumConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

    }
}
