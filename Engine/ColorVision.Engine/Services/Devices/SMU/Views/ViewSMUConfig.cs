using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.PropertyEditor;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.SMU.Views
{
    public class ViewSMUConfig : ViewConfigBase, IConfig
    {
        public static ViewSMUConfig Instance => ConfigService.Instance.GetRequiredService<ViewSMUConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

    }
}
