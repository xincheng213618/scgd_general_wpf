using ColorVision.Engine.Services.Devices.CfwPort.Local;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.CfwPort;

public partial class EditCfwPort : Window
{
    private readonly DeviceCfwPort device;
    internal DeviceConfigurationTarget OpeningTarget { get; }
    public CfwPortConfigurationDraft Draft { get; }

    internal EditCfwPort(DeviceCfwPort device)
    {
        this.device = device;
        OpeningTarget = device.CaptureConfigurationTarget();
        Draft = CfwPortConfigurationDraft.From(device.Config, device.DisplayConfig);
        InitializeComponent();
        this.ApplyCaption();
        DataContext = Draft;
        AddFields(ModeFields, nameof(Draft.UseLocalCfwPort), nameof(Draft.IsAutoOpen));
        AddFields(ConnectionFields, nameof(Draft.SzComName), nameof(Draft.BaudRate), nameof(Draft.Timeout));
        AddFields(PositionFields, nameof(Draft.Ports), nameof(Draft.RetryCount), nameof(Draft.Delay));
        AddFields(LicenseFields, nameof(Draft.SN));
    }

    private void AddFields(Panel panel, params string[] names)
    {
        foreach (string name in names)
        {
            var field = PropertyEditorHelper.GenProperties(Draft, name, Properties.Resources.ResourceManager);
            field.Margin = new Thickness(0, 3, 0, 3);
            field.MinHeight = 28;
            panel.Children.Add(field);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();
        if (!device.CanConfigure)
        {
            StatusText.Text = EngineLocalization.Get("当前权限不足，无法保存设备配置。");
            return;
        }
        if (!device.TryAdmitConfigurationEdit(OpeningTarget, this)) return;
        if (Draft.UseLocalCfwPort)
        {
            try { LocalCfwPortConnection.From(Draft).Validate(); }
            catch (Exception error) when (error is ArgumentException)
            {
                StatusText.Text = error.Message;
                return;
            }
        }
        DialogResult = true;
    }
}
