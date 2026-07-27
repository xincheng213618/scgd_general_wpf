using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing;

public sealed class FlowEngineConfig : ViewModelBase, IConfig
{
    public static FlowEngineConfig Instance => ConfigService.Instance.GetRequiredService<FlowEngineConfig>();

    [JsonIgnore]
    public RelayCommand EditCommand { get; }

    [Display(Name = "Engine_PG_EditSavePrompt", ResourceType = typeof(Properties.Resources))]
    public bool IsAutoEditSave { get => _isAutoEditSave; set => SetProperty(ref _isAutoEditSave, value); }
    private bool _isAutoEditSave;

    public int LastSelectFlow { get => _lastSelectFlow; set => SetProperty(ref _lastSelectFlow, value); }
    private int _lastSelectFlow;

    [Browsable(false)]
    public int TemplateFlowParamsIndex { get => _templateFlowParamsIndex; set => SetProperty(ref _templateFlowParamsIndex, value); }
    private int _templateFlowParamsIndex;

    [Browsable(false)]
    public int TemplateLargeFlowParamsIndex { get => _templateLargeFlowParamsIndex; set => SetProperty(ref _templateLargeFlowParamsIndex, value); }
    private int _templateLargeFlowParamsIndex;

    public FlowEngineConfig()
    {
        EditCommand = new RelayCommand(_ => new PropertyEditorWindow(this)
        {
            Owner = Application.Current.GetActiveWindow(),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        }.ShowDialog());
    }
}
