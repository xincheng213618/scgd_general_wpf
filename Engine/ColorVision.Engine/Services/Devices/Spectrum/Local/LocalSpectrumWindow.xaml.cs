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
    private readonly ViewSpectrum results;
    private LocalSpectrumCapture? latest;
    private LocalSpectrumNode? node;
    internal LocalSpectrumWindow(DeviceSpectrum device)
    {
        this.device = device;
        InitializeComponent();
        DataContext = device;
        results = new ViewSpectrum(device);
        ResultHost.Content = results;
        device.LocalResultAvailable += ResultAvailable;
        device.LocalCaptureFailed += CaptureFailed;
        Closed += (_, _) => { device.LocalResultAvailable -= ResultAvailable; device.LocalCaptureFailed -= CaptureFailed; results.Dispose(); };
    }

    internal void AttachNode(LocalSpectrumNode? value)
    {
        node = value;
        ApplyNodeButton.Visibility = node == null ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ResultAvailable(LocalSpectrumCapture result, bool continuous)
    {
        latest = result;
        if (continuous) results.SetLocalPreview(DeviceSpectrum.CreateLocalSpectrumView(result));
        else results.AddViewResultSpectrum(DeviceSpectrum.CreateLocalSpectrumView(result));
        Message.Text = $"采集完成  {result.CapturedAt:HH:mm:ss}  积分 {result.IntegralTime:0.###} ms";
    }
    private void CaptureFailed(string error) => Dispatcher.BeginInvoke(() => Message.Text = error);

    private async void Command_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string command }) return;
        try
        {
            Message.Text = "正在执行…";
            MsgRecord record = device.RunLocalSpectrumCommand(command);
            MsgRecordState state = await ScheduledDeviceJobHelper.WaitForTerminalStateAsync(record, TimeSpan.FromMinutes(20), CancellationToken.None);
            Message.Text = state == MsgRecordState.Success ? "操作完成" : record.MsgReturn?.Message ?? "操作失败";
        }
        catch (Exception ex) { Message.Text = ex.Message; }
    }

    private void ApplyNode_Click(object sender, RoutedEventArgs e)
    {
        if (node == null) return;
        node.IntegralTime = (float)device.DisplayConfig.IntTime;
        node.NumberOfAverage = device.DisplayConfig.AveNum;
        node.AutoIntegration = device.DisplayConfig.IsAutoIntTime;
        node.AutoInitDark = device.DisplayConfig.IsAutoDark;
        node.SelfAdaptionInitDark = device.DisplayConfig.IsShutter;
        node.Eqe = device.DisplayConfig.IsLuminousFluxMode;
        node.AFactor = device.DisplayConfig.Divisor;
        node.Voltage = (float)device.DisplayConfig.V;
        node.Current = (float)device.DisplayConfig.I;
        Message.Text = "参数已应用到节点，请保存流程。";
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (latest == null) { Message.Text = "请先采集一条结果。"; return; }
        var dialog = new SaveFileDialog { Filter = "光谱结果 (*.json)|*.json", FileName = $"Spectrum-{latest.CapturedAt:yyyyMMdd-HHmmss}.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(latest, Formatting.Indented)); Message.Text = "结果已导出。"; }
        catch (Exception ex) { Message.Text = ex.Message; }
    }
}
