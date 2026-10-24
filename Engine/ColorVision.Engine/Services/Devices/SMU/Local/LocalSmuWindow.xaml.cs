using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.UI;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlowEngineLib;

namespace ColorVision.Engine.Services.Devices.SMU.Local;

public partial class LocalSmuWindow : Window
{
    private readonly DeviceSMU device;
    private readonly ViewSMU curves = new();
    private LocalSmuSettings settings;
    private LocalSmuNode? node;
    private LocalSmuCapture? latest;
    private CancellationTokenSource? operation;
    internal LocalSmuSettings Settings => settings;

    internal LocalSmuWindow(DeviceSMU device)
    {
        this.device = device;
        settings = device.DisplayConfig.LocalSettings?.Clone() ?? new();
        InitializeComponent();
        ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
        DeviceTitle.Text = $"本地源表 · {device.Config.Name}";
        CurveHost.Content = curves;
        BuildEditors(); RefreshIdentity();
        device.LocalResultAvailable += ResultAvailable;
        Closing += (_, e) =>
        {
            if (operation == null) return;
            e.Cancel = true; operation.Cancel(); Message.Text = "正在等待 SDK 返回并清理输出，完成后可关闭窗口。";
        };
        Closed += (_, _) => device.LocalResultAvailable -= ResultAvailable;
    }

    internal void AttachNode(LocalSmuNode? value)
    {
        if (operation != null) { Message.Text = "正在执行，完成后再切换节点。"; return; }
        if (value != null)
        {
            settings = value.LocalSettings?.Clone() ?? new LocalSmuSettings
            {
                FourWire = device.Config.Is4Wire, Front = device.Config.IsFront, Delay = device.Config.DelayTime,
                SourceRange = value.IsAutoRng ? 0 : value.SrcRng, MeasureRange = value.IsAutoRng ? 0 : value.LmtRng
            };
            settings.Channel = (Dao.SMUChannelType)(int)value.Channel;
            settings.IsSourceV = value.Source == SourceType.Voltage_V;
            settings.SourceValue = value.MeasureValue; settings.Limit = value.LimitValue;
            settings.CloseOutput = value.CloseOutputOnFinish;
            BuildEditors();
        }
        node = value;
        ApplyNodeButton.Visibility = value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BuildEditors()
    {
        ParameterTabs.Items.Clear();
        foreach (var group in PropertyEditorHelper.GetEditableProperties(typeof(LocalSmuSettings))
            .GroupBy(property => property.GetCustomAttribute<CategoryAttribute>()?.Category ?? "参数"))
        {
            var panel = new StackPanel { Margin = new Thickness(12) };
            foreach (var property in group)
            {
                DockPanel editor = PropertyEditorHelper.GenProperties(property, settings);
                editor.ToolTip = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
                panel.Children.Add(editor);
            }
            ParameterTabs.Items.Add(new TabItem { Header = group.Key, Content = new ScrollViewer
                { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = panel } });
        }
    }

    private void RefreshIdentity()
    {
        Identity.Text = string.IsNullOrWhiteSpace(device.LocalSession.Idn) ? "尚未连接。连接只查询型号；点击执行才会开启输出。"
            : $"{device.LocalSession.Idn}  ·  {device.LocalSession.Status}";
        Capabilities.Text = device.LocalSession.LocalFamily switch
        {
            1 => "2400：V/I、直流电阻（自动/手动/偏置补偿）、线性/对数/列表/源记忆扫描、2/4 线及 Ohms Guard、SDM 间接脉冲、高阻/零/Guard 关闭状态。",
            2 => "2430：2400 系列的直流测量与扫描，以及专用脉冲输出（当前仅输出，脉宽 0.15～5 ms）。专用脉冲不能并发测量 V/I。",
            3 => "原始 2600：V/I、直流电阻、功率、线性/对数/列表扫描、2/4 线和关闭状态。本协议不在此型号上启用 A/B 定时脉冲。",
            4 => "2600 A/B：V/I、直流电阻、功率、线性/对数/列表扫描、仪器定时脉冲、2/4 线及高电容模式。通道数、量程和脉冲指标取决于实际型号。",
            _ => "尚未识别到支持高级功能的仪器。可先编辑参数；连接后按 IDN 和原生 SDK 判断能力。未知型号或旧 DLL 会明确拒绝执行。"
        };
    }

    private async void Command_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string command }) return;
        if (operation != null)
        {
            if (command is "Close" or "CloseOutput") { operation.Cancel(); Message.Text = "已请求停止；等待 SDK 返回后关闭输出并释放连接。"; }
            return;
        }
        Keyboard.ClearFocus();
        var snapshot = settings.Clone();
        operation = new CancellationTokenSource();
        ParameterTabs.IsEnabled = false; ExecuteButton.IsEnabled = false;
        LocalSmuCapture? acquired = null;
        try
        {
            if (command == "Advanced") snapshot.Validate(device.Config.DevType, device.DisplayConfig.IsUseLimitSigned);
            Message.Text = "正在执行…";
            var connection = LocalSmuConnection.From(device.Config, (int)device.DisplayConfig.LocalBaudRate);
            var parameters = new LocalSmuParameters { Channel = snapshot.Channel, AdvancedSettings = snapshot };
            var capture = await device.ExecuteLocalSmuAsync(command, connection, parameters, command == "Advanced",
                device.DisplayConfig.IsUseLimitSigned, operation.Token);
            if (capture != null)
            {
                acquired = capture;
                // Retain acquired data if persistence fails, while still reporting the failure.
                device.PublishLocalSmu(capture, 0);
                int id = await System.Threading.Tasks.Task.Run(() => LocalSmuResultService.Save(capture, device.Code));
                operation.Token.ThrowIfCancellationRequested();
                Message.Text = capture.HasMeasurements ? $"完成：{capture.Voltages.Length} 点，{capture.TotalTime} ms，结果 ID {id}。" : "输出设置完成；本次未执行测量。";
            }
            else
            {
                if (command is "Close" or "CloseOutput") device.ClearLocalSmuReadings(snapshot.Channel, command == "Close");
                Message.Text = "操作完成。";
            }
        }
        catch (OperationCanceledException)
        {
            Message.Text = "已取消。";
            if (acquired != null && device.LocalSession.IsOpen)
            {
                try { await device.LocalSession.CloseOutputAsync(acquired.Parameters.Channel, expectedGeneration: acquired.Generation); }
                catch (Exception ex) { Message.Text = $"已取消；输出清理未完成：{ex.Message}"; }
            }
        }
        catch (Exception ex) { Message.Text = ex.Message; MQTTServiceBase.log.Error("本地源表操作失败", ex); }
        finally { operation.Dispose(); operation = null; ParameterTabs.IsEnabled = true; ExecuteButton.IsEnabled = true; RefreshIdentity(); }
    }

    private void ResultAvailable(LocalSmuCapture capture)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => ResultAvailable(capture)); return; }
        latest = capture;
        Readings.ItemsSource = Enumerable.Range(0, capture.Voltages.Length).Select(i => new
        {
            Index = i + 1, Source = capture.Details?.SourceValues[i] ?? capture.Parameters.MeasureValue,
            Voltage = capture.Voltages[i], Current = capture.Currents[i],
            Resistance = capture.Details?.Settings.Measurement == SmuMeasurement.Resistance ? capture.Details.Resistance[i]
                : LocalSmuDetails.ResistanceFromVI(capture.Voltages[i], capture.Currents[i]),
            Power = capture.Voltages[i] * capture.Currents[i] / 1000, Status = capture.Details?.Status[i]
        }).ToArray();
        curves.ClearResults();
        if (capture.HasMeasurements) curves.AddViewResultSMU(LocalSmuResultService.CreateView(capture, device.Code, 0));
    }
    private void ConnectionSettings_Click(object sender, RoutedEventArgs e)
    {
        if (operation == null) device.EditCommand.Execute(null);
    }
    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (operation == null) { Command_Click(new Button { Tag = "CloseOutput" }, e); return; }
        operation?.Cancel();
        if (operation != null) Message.Text = "已请求取消；等待正在执行的 SDK 调用返回后清理输出。";
    }
    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Keyboard.ClearFocus(); settings.Validate(device.Config.DevType, device.DisplayConfig.IsUseLimitSigned);
            device.DisplayConfig.LocalSettings = settings.Clone();
            ConfigHandler.GetInstance().Save<DisplayConfigManager>(); Message.Text = "本地源表参数已保存。";
        }
        catch (Exception ex) { Message.Text = ex.Message; }
    }
    private void ApplyNode_Click(object sender, RoutedEventArgs e)
    {
        if (node == null || operation != null) return;
        try
        {
            Keyboard.ClearFocus(); settings.Validate(device.Config.DevType, device.DisplayConfig.IsUseLimitSigned);
            node.LocalSettings = settings.Clone(); node.Channel = (SMUChannelType)(int)settings.Channel;
            node.Source = settings.IsSourceV ? SourceType.Voltage_V : SourceType.Current_I;
            node.MeasureValue = settings.SourceValue; node.LimitValue = settings.Limit;
            node.CloseOutputOnFinish = settings.CloseOutput;
            Message.Text = "参数已应用到节点，请保存流程。";
        }
        catch (Exception ex) { Message.Text = ex.Message; }
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (latest == null) { Message.Text = "请先执行一次测量或输出操作。"; return; }
        var dialog = new SaveFileDialog { Filter = "源表结果 (*.json)|*.json", FileName = $"SMU-{latest.CapturedAt:yyyyMMdd-HHmmss}.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(new { latest.CapturedAt, latest.TotalTime,
                VoltageUnit = "V", CurrentUnit = "mA", ResistanceUnit = "Ohm", PowerUnit = "W", latest.HasMeasurements,
                Voltage = latest.Voltages, Current = latest.Currents, Power = latest.Voltages.Zip(latest.Currents, (v, i) => v * i / 1000), latest.Details }, Formatting.Indented));
            Message.Text = "结果已导出。";
        }
        catch (Exception ex) { Message.Text = ex.Message; }
    }
}
