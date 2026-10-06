using ColorVision.Common.MVVM;
using ColorVision.UI;
using FlowEngineLib.End;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing;

public sealed class FlowEngineConfig : ViewModelBase, IConfig
{
    public static FlowEngineConfig Instance => ConfigService.Instance.GetRequiredService<FlowEngineConfig>();

    public int LastSelectFlow { get => _lastSelectFlow; set => SetProperty(ref _lastSelectFlow, value); }
    private int _lastSelectFlow;

    [DisplayName("Flow Post-processing Time (ms)")]
    [ConfigSetting(Name = "Flow Post-processing Time (ms)", Order = 50, Section = ConfigSettingConstants.SectionBasic)]
    public int FlowEndDelayMilliseconds
    {
        get => _flowEndDelayMilliseconds;
        set
        {
            int delay = value is > 0 and <= 1000 ? value : 0;
            CVEndNode.EndDelayMilliseconds = delay;
            SetProperty(ref _flowEndDelayMilliseconds, delay);
        }
    }
    private int _flowEndDelayMilliseconds;

    [Browsable(false)]
    public int TemplateFlowParamsIndex { get => _templateFlowParamsIndex; set => SetProperty(ref _templateFlowParamsIndex, value); }
    private int _templateFlowParamsIndex;
}

public sealed class FlowEngineConfigInitializer : InitializerBase
{
    public override Task InitializeAsync()
    {
        CVEndNode.EndDelayMilliseconds = FlowEngineConfig.Instance.FlowEndDelayMilliseconds;
        return Task.CompletedTask;
    }
}
