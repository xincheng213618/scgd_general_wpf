using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.Engine;

public sealed class ExperimentalFeaturesConfig : ViewModelBase, IConfig
{
    public static ExperimentalFeaturesConfig Instance => ConfigService.Instance.GetRequiredService<ExperimentalFeaturesConfig>();

    [DisplayName("实验功能")]
    [ConfigSetting(Order = 55, Section = ConfigSettingConstants.SectionBasic)]
    public bool IsEnabled { get => isEnabled; set => SetProperty(ref isEnabled, value); }
    private bool isEnabled;
}
