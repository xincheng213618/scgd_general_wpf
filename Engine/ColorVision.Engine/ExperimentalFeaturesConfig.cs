using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine;

public sealed class ExperimentalFeaturesConfig : ViewModelBase, IConfig
{
    public static ExperimentalFeaturesConfig Instance => ConfigService.Instance.GetRequiredService<ExperimentalFeaturesConfig>();

    [DisplayName("实验功能")]
    [ConfigSetting(Order = 55, Section = ConfigSettingConstants.SectionOther)]
    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (!SetProperty(ref isEnabled, value) || MenuService.Instance is not { } menuService
                || Application.Current?.Dispatcher is not { HasShutdownStarted: false } dispatcher) return;

            _ = dispatcher.BeginInvoke(() => menuService.RefreshMenuItemsByGuid(MenuItemConstants.Tool));
        }
    }
    private bool isEnabled;
}
