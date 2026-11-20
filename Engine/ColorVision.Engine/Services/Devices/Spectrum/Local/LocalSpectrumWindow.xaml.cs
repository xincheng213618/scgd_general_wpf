using LocalizedText = global::ColorVision.Engine.EngineLocalization;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

public partial class LocalSpectrumWindow : Window
{
    private readonly DeviceSpectrum device;
    private readonly DeviceConfigurationTarget configurationTarget;
    private readonly ViewSpectrum results;
    private readonly CancellationTokenSource lifetime = new();
    private LocalSpectrumCapture? latest;
    private LocalSpectrumNode? node;
    private bool closed;
    private bool CanUseWindow => !closed && !device.IsDisposed;
    internal LocalSpectrumWindow(DeviceSpectrum device)
    {
        this.device = device;
        configurationTarget = device.CaptureConfigurationTarget();
        InitializeComponent();
        ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
        DataContext = device;
        results = new ViewSpectrum(device);
        ResultHost.Content = results;
        device.LocalResultAvailable += ResultAvailable;
        device.LocalCaptureFailed += CaptureFailed;
    }

    protected override void OnClosed(EventArgs e)
    {
        closed = true;
        lifetime.Cancel();
        lifetime.Dispose();
        device.LocalResultAvailable -= ResultAvailable;
        device.LocalCaptureFailed -= CaptureFailed;
        node = null;
        try { results.Dispose(); }
        finally { base.OnClosed(e); }
    }

    internal void CloseForDeviceRetirement()
    {
        if (!closed) Close();
    }

    internal void AttachNode(LocalSpectrumNode? value)
    {
        node = value;
        ApplyNodeButton.Visibility = node == null ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ResultAvailable(LocalSpectrumCapture result, bool continuous)
    {
        if (!CanUseWindow) return;
        latest = result;
        results.AddViewResultSpectrum(DeviceSpectrum.CreateLocalSpectrumView(result));
        Message.Text = LocalizedText.Format($"采集完成  {result.CapturedAt:HH:mm:ss}  积分 {result.IntegralTime:0.###} ms");
    }
    private void CaptureFailed(string error) => Dispatcher.BeginInvoke(() =>
    {
        if (CanUseWindow) Message.Text = error;
    });

    private async void Command_Click(object sender, RoutedEventArgs e)
    {
        if (!CanUseWindow || sender is not Button { Tag: string command }) return;
        try
        {
            CancellationToken cancellation = lifetime.Token;
            Message.Text = command is "GetDataAutoStop" or "Close"
                ? "正在停止，等待当前采集返回…" : LocalizedText.Get("正在执行…");
            string eventName = command;
            if (command is "ShutterDoOpen" or "ShutterDoClose")
            {
                bool transmitting = command == "ShutterDoClose";
                eventName = transmitting != device.Config.ShutterCfg.OpenForDark ? "ShutterDoOpen" : "ShutterDoClose";
            }
            MsgRecord record = device.RunLocalSpectrumCommand(eventName);
            MsgRecordState state = await ScheduledDeviceJobHelper.WaitForTerminalStateAsync(record, TimeSpan.FromMinutes(20), cancellation);
            if (!CanUseWindow) return;
            Message.Text = state == MsgRecordState.Success
                ? LocalizedText.Get(command switch
                {
                    "ResetDark" => "校零数据已重置，请重新采集。",
                    "ShutterDoOpen" => "遮光指令已完成。",
                    "ShutterDoClose" => "通光指令已完成。",
                    _ => "操作完成"
                })
                : record.MsgReturn?.Message ?? LocalizedText.Get("操作失败");
        }
        catch (Exception ex) { if (CanUseWindow) Message.Text = ex.Message; }
    }

    private void ApplyNode_Click(object sender, RoutedEventArgs e)
    {
        if (!CanUseWindow || node == null || !device.IsConfigurationTargetCurrent(configurationTarget)) return;
        node.IntegralTime = (float)device.DisplayConfig.IntTime;
        node.NumberOfAverage = device.DisplayConfig.AveNum;
        node.AutoIntegration = device.DisplayConfig.IsAutoIntTime;
        node.IsWithND = device.DisplayConfig.IsWithND;
        node.AutoInitDark = device.DisplayConfig.IsAutoDark;
        node.SelfAdaptionInitDark = device.DisplayConfig.IsShutter;
        node.Eqe = device.DisplayConfig.IsLuminousFluxMode;
        node.AFactor = device.DisplayConfig.Divisor;
        var inputs = device.DisplayConfig.ReadEffectiveElectricalInputs();
        node.Voltage = (float)inputs.Voltage;
        node.Current = (float)(inputs.CurrentInAmperes.HasValue || device.IsLocalSpectrumBackend
            ? inputs.NativeCurrentAmps : inputs.LegacyRemoteCurrent / 1000.0);
        Message.Text = LocalizedText.Get("参数已应用到节点，请保存流程。");
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!CanUseWindow) return;
        if (latest is not { } captured) { Message.Text = LocalizedText.Get("请先采集一条结果。"); return; }
        var dialog = new SaveFileDialog { Filter = "光谱结果 (*.json)|*.json", FileName = $"Spectrum-{captured.CapturedAt:yyyyMMdd-HHmmss}.json" };
        if (dialog.ShowDialog(this) != true || !CanUseWindow) return;
        try { File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(captured, Formatting.Indented)); Message.Text = LocalizedText.Get("结果已导出。"); }
        catch (Exception ex) { Message.Text = ex.Message; }
    }
}
