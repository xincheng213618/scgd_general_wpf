using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices;

public partial class CameraBoundDeviceConfigurationWindow : Window
{
    private readonly Func<Window, bool> submit;

    internal CameraBoundDeviceConfigurationDraft Draft { get; }

    internal CameraBoundDeviceConfigurationWindow(Window? owner, CameraBoundDeviceConfigurationDraft draft, Func<Window, bool> submit, params string[] additionalFields)
    {
        Draft = draft;
        this.submit = submit;
        InitializeComponent();
        this.ApplyCaption();
        Owner = owner;

        AddField(nameof(CameraBoundDeviceConfigurationDraft.Name));
        AddField(nameof(CameraBoundDeviceConfigurationDraft.CameraCode));
        foreach (string name in additionalFields)
            AddField(name);
    }

    private void AddField(string name)
    {
        var field = PropertyEditorHelper.GenProperties(Draft, name, Properties.Resources.ResourceManager);
        field.Margin = new Thickness(0, 3, 0, 5);
        field.MinHeight = 28;
        ConfigurationFields.Children.Add(field);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();
        try
        {
            if (submit(this))
                Close();
        }
        catch (Exception error)
        {
            MQTTServiceBase.log.Error("保存设备配置失败。", error);
            StatusText.Text = error.Message;
            StatusText.Visibility = Visibility.Visible;
        }
    }
}

internal class CameraBoundDeviceConfigurationDraft
{
    [DisplayName(nameof(Properties.Resources.FrameDisplayName))]
    public string Name { get; set; } = string.Empty;

    [DisplayName(nameof(Properties.Resources.PhysicalCamera)), PropertyEditorType(typeof(TextSNPropertiesEditor))]
    public string? CameraCode { get; set; }
}
