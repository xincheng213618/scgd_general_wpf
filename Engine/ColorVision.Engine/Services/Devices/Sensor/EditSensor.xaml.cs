using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Themes;
using ColorVision.UI;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Sensor;

public partial class EditSensor : Window
{
    private readonly DeviceConfigurationTarget openingTarget;
    internal DeviceConfigurationTarget OpeningConfigurationTarget => openingTarget;
    public DeviceSensor Device { get; }
    public SensorConfigurationDraft Draft { get; }
    public ConfigSensor EditConfig => Draft.Config;

    public EditSensor(DeviceSensor device)
    {
        Device = device;
        openingTarget = device.CaptureConfigurationTarget();
        Draft = new SensorConfigurationDraft(device.Config, device.DisplayConfig);
        InitializeComponent();
        this.ApplyCaption();
        DataContext = this;
        AddFields(DeviceFields, EditConfig, nameof(ConfigSensor.Name), nameof(ConfigSensor.IsAutoOpen));
        AddFields(LocalFields, Draft.DisplayConfig, nameof(DisplaySensorConfig.UseLocalSensor));
        AddFields(LocalConnectionFields, Draft.DisplayConfig, nameof(DisplaySensorConfig.ConnectTimeout));
        AddFields(SerialOptionsFields, Draft.DisplayConfig, nameof(DisplaySensorConfig.DataBits), nameof(DisplaySensorConfig.Parity), nameof(DisplaySensorConfig.StopBits));
        AddFields(SerialSignalFields, Draft.DisplayConfig, nameof(DisplaySensorConfig.DtrEnable), nameof(DisplaySensorConfig.RtsEnable));
        var dictionary = SensorTemplateDictionaryService.GetDefaultDictionary();
        ComboBoxSensor.ItemsSource = BuildCategoryChoices(dictionary, EditConfig.Category);
        ComboBoxSensor.SelectedValue = EditConfig.Category;
        ComboBoxPort.ItemsSource = new[] { 300, 600, 1200, 2400, 4800, 9600, 14400, 19200, 38400, 57600, 115200 };
        ComboBoxSerial.ItemsSource = SerialPort.GetPortNames().Append(EditConfig.Addr).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToArray();
        if (MySqlSetting.IsConnect) CameraPhyID.ItemsSource = PhyCameraManager.GetInstance().PhyCameras;
    }

    internal static List<SysDictionaryModModel> BuildCategoryChoices(SysDictionaryModModel? defaultDictionary, string? configuredCode)
    {
        List<SysDictionaryModModel> categories = defaultDictionary == null ? [] : [defaultDictionary];
        if (!string.IsNullOrWhiteSpace(configuredCode) && configuredCode != defaultDictionary?.Code)
            categories.Add(new SysDictionaryModModel { Code = configuredCode });
        return categories;
    }

    private static void AddFields(Panel panel, object config, params string[] names)
    {
        foreach (string name in names)
        {
            var field = PropertyEditorHelper.GenProperties(config, name, Properties.Resources.ResourceManager);
            field.Margin = new Thickness(0, 3, 0, 3);
            field.MinHeight = 28;
            if (panel.Name is "SerialOptionsFields" or "SerialSignalFields")
                foreach (UIElement child in field.Children)
                    if (child is Control control) control.MinWidth = 100;
            panel.Children.Add(field);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();
        if (!Device.TryAdmitConfigurationEdit(openingTarget, this)) return;
        if (ComboBoxSensor.SelectedValue is string code)
            EditConfig.Category = code;
        if (!Draft.TryApply(Device.Config, Device.DisplayConfig, out string error))
        {
            StatusText.Text = error;
            return;
        }
        DialogResult = true;
    }
}
