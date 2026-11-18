using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ToolPlugins.ThirdPartyApps;

public partial class SscomPresetWindow : Window
{
    private IReadOnlyList<SscomCommandPreset> _commands = [];

    public SscomPresetWindow()
    {
        InitializeComponent();
        this.ApplyCaption();
        ProfileList.ItemsSource = GetProfiles().ToArray();
        ProfileList.SelectedIndex = 0;
    }

    internal static void ShowWindow() => new SscomPresetWindow { Owner = Application.Current.GetActiveWindow() }.ShowDialog();

    private IEnumerable<PresetProfile> GetProfiles()
    {
        yield return new(DisplayText.Get("通用传感器 · PG 常用指令"), false, SscomSensorPresets.BuildDefaults);
        yield return new("ARVR Pro · TCP/JSON", true,
            () => SscomArvrPresets.Build(SerialNumber.Text));

        if (TemplateSensor.Params.TryGetValue("Sensor.Default", out var loadedTemplates))
        {
            foreach (var template in loadedTemplates)
            {
                yield return new(DisplayText.Format($"通用传感器 · {template.Key}"), false,
                    () => SscomSensorPresets.Build(template.Key, template.Value.ModDetailModels
                        .Where(detail => detail.IsEnable && !detail.IsDelete).Select(detail => detail.ValueA)));
            }
        }
    }

    private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void SerialNumber_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (ProfileList?.SelectedItem is not PresetProfile profile) return;
        ArvrSettings.Visibility = profile.IsTcp ? Visibility.Visible : Visibility.Collapsed;
        _commands = [];
        CommandPreview.ItemsSource = null;
        try
        {
            if (profile.IsTcp && string.IsNullOrWhiteSpace(SerialNumber.Text))
                throw new ArgumentException(DisplayText.Get("请输入用于测试的 SN。"));
            _commands = profile.Build();
            if (_commands.Count > SscomPresetIni.MaxCommands)
                throw new InvalidDataException(DisplayText.Format($"SSCOM 单个配置最多支持 {SscomPresetIni.MaxCommands} 条指令。"));
            CommandPreview.ItemsSource = _commands.Select(command => new CommandRow(command.Name, profile.IsTcp
                ? Encoding.UTF8.GetString(command.Payload)
                : SensorCommandTextFormatter.ToReadableText(Convert.ToHexString(command.Payload), true))).ToArray();
            Status.Text = _commands.Count > 0
                ? DisplayText.Get("预设会添加到 SSCOM 的「多条字符串发送」列表。")
                : DisplayText.Get("此模板没有已启用的指令。");
        }
        catch (Exception ex)
        {
            _commands = [];
            Status.Text = ex.Message;
        }
        ApplyButton.IsEnabled = _commands.Count > 0;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not PresetProfile profile || _commands.Count == 0) return;
        try
        {
            SscomTcpEndpoint? endpoint = null;
            if (profile.IsTcp)
            {
                if (!int.TryParse(ServerPort.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int port))
                    throw new ArgumentException(DisplayText.Get("请输入有效的端口号。"));
                endpoint = new SscomTcpEndpoint(ServerAddress.Text.Trim(), port);
            }

            PortableAppProvider.LaunchSscom(_commands, endpoint);
            Close();
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }

    private sealed record PresetProfile(string Name, bool IsTcp, Func<IReadOnlyList<SscomCommandPreset>> Build);
    private sealed record CommandRow(string Name, string Request);
}
