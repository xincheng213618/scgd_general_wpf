using ColorVision.Engine.Cache;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Services;

public partial class DeviceDataPathWindow : Window
{
    private readonly DeviceDataPathConfiguration configuration;
    private readonly FileServerCfg draft;

    internal DeviceDataPathWindow(DeviceDataPathConfiguration configuration)
    {
        this.configuration = configuration;
        draft = new FileServerCfg { DataBasePath = configuration.InitialPath };
        InitializeComponent();
        this.ApplyCaption();

        var pathEditor = PropertyEditorHelper.GenProperties(draft, nameof(FileServerCfg.DataBasePath));
        pathEditor.Margin = new Thickness(0);
        DataPathEditor.Content = pathEditor;
        draft.PropertyChanged += (_, _) => DataPathStatus.Visibility = Visibility.Collapsed;
    }

    internal bool HasApplied { get; private set; }

    private void ApplyDataPath_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();
        try
        {
            DeviceDataPathApplyResult result = configuration.Apply(draft.DataBasePath);
            HasApplied = true;
            draft.DataBasePath = configuration.InitialPath;
            ShowResult(result);
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or IOException or ArgumentException)
        {
            ShowStatus(exception.Message);
        }
    }

    private void ShowResult(DeviceDataPathApplyResult result)
    {
        static string Format(string key, object value) => string.Format(CultureInfo.CurrentCulture, EngineLocalization.Get(key), value);

        var messages = new List<string>
        {
            result.UpdatedDevices == 0 ? EngineLocalization.Get("保存成功")
                : Format("DeviceDataPathUpdated", result.UpdatedDevices)
        };
        if (result.ReloadRequestedDevices > 0)
            messages.Add(Format("DeviceDataPathReloadRequested", result.ReloadRequestedDevices));
        if (result.ReloadPendingDevices.Length > 0)
            messages.Add(Format("DeviceDataPathReloadPending", string.Join(", ", result.ReloadPendingDevices)));
        if (result.CameraReconnectDevices.Length > 0)
            messages.Add(Format("DeviceDataPathCameraPending", string.Join(", ", result.CameraReconnectDevices)));

        ShowStatus(string.Join(" ", messages));
    }

    private void ShowStatus(string message)
    {
        DataPathStatus.Text = message;
        DataPathStatus.Visibility = Visibility.Visible;
    }
}
