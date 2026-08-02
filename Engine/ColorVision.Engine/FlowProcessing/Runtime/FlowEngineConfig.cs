using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.Engine.FlowProcessing;

public sealed class FlowEngineConfig : ViewModelBase, IConfig
{
    public static FlowEngineConfig Instance => ConfigService.Instance.GetRequiredService<FlowEngineConfig>();

    public int LastSelectFlow { get => _lastSelectFlow; set => SetProperty(ref _lastSelectFlow, value); }
    private int _lastSelectFlow;

    [Browsable(false)]
    public int TemplateFlowParamsIndex { get => _templateFlowParamsIndex; set => SetProperty(ref _templateFlowParamsIndex, value); }
    private int _templateFlowParamsIndex;
}
