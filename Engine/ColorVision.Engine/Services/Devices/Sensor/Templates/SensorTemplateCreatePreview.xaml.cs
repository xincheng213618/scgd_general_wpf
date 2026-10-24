using ColorVision.Engine.Templates;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates;

public partial class SensorTemplateCreatePreview : UserControl, ITemplateUserControl
{
    public SensorTemplateCreatePreview()
    {
        InitializeComponent();
        CommandEditor.CommandSelectionChanged += (_, _) => EchoPanel.DataContext = CommandEditor.SelectedCommand;
    }

    public void SetParam(object param)
    {
        if (param is not SensorParam sensor) throw new ArgumentException("需要传感器指令模板。", nameof(param));
        SensorTemplatePreset? preset = sensor.CreationPreset;
        DefaultGuidancePanel.Visibility = preset == null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        PresetTitle.Text = preset?.Source.Title ?? "自定义传感器指令";
        PurposeText.Text = preset?.Purpose ?? "按设备协议填写发送指令和预期回包，也可以从左侧选择已有业务预设。";
        ModuleGuideText.Text = preset?.ModuleGuide ?? "流程选择“本地设备 → 本地通用传感器”，选择目标传感器设备和本模板。连接参数在设备配置中设置。";
        DeviceGuideText.Text = preset?.DeviceGuide ?? "先核对控制器型号和通信协议；PG 文本、GECS 帧、SA 灯控的指令不同。未知设备请根据厂家协议填写，或导入已验证的模板。";
        CommandEditor.SetParam(sensor);
        CommandEditorExpander.IsExpanded = preset == null;
    }
}
