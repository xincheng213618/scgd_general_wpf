using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Themes;
using ColorVision.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.PG;

public partial class EditPG : Window
{
    private readonly ConfigPG target;
    public PGConfigurationDraft Draft { get; }
    public ConfigPG EditConfig => Draft.Config;

    public EditPG(DevicePG device) : this(device.Config, GetCategories(device))
    {
        Icon = device.Icon;
        if (MySqlSetting.IsConnect)
        {
            CameraPhyID.ItemsSource = PhyCameraManager.GetInstance().PhyCameras;
            CameraPhyID.ItemTemplate = TextSNPropertiesEditor.CreateCameraSnItemTemplate();
        }
    }

    internal EditPG(ConfigPG source, IEnumerable<string> categories)
    {
        target = source;
        Draft = new PGConfigurationDraft(source);
        InitializeComponent();
        this.ApplyCaption();
        DataContext = this;
        foreach (string name in new[] { nameof(ConfigPG.Name), nameof(ConfigPG.IsAutoOpen) })
        {
            var field = PropertyEditorHelper.GenProperties(EditConfig, name, Properties.Resources.ResourceManager);
            field.Margin = new Thickness(0, 3, 0, 3);
            field.MinHeight = 28;
            DeviceFields.Children.Add(field);
        }
        PgCategory.ItemsSource = categories.Append(EditConfig.Category).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToArray();
        SerialBaudRate.ItemsSource = new[] { 300, 600, 1200, 2400, 4800, 9600, 14400, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };
        I2CSpeed.ItemsSource = new Dictionary<CH341_Stream_Speed, string>
        {
            [CH341_Stream_Speed.CH341_20KHZ] = "20 kHz",
            [CH341_Stream_Speed.CH341_100KHZ] = "100 kHz",
            [CH341_Stream_Speed.CH341_400KHZ] = "400 kHz",
            [CH341_Stream_Speed.CH341_750KHZ] = "750 kHz"
        };
    }

    private static IEnumerable<string> GetCategories(DevicePG device)
    {
        device.DService.ReLoadCategoryLib();
        return device.DService.PGCategoryLib.Keys;
    }

    internal bool TryApply(out string error)
    {
        Keyboard.ClearFocus();
        if (HasInvalidInput(EditorContent))
        {
            error = "请修正输入框中的无效数值。";
            return false;
        }
        return Draft.TryApply(target, out error);
    }

    private static bool HasInvalidInput(DependencyObject root)
    {
        if (root is UIElement element && element.Visibility != Visibility.Visible) return false;
        if (Validation.GetHasError(root)) return true;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (HasInvalidInput(VisualTreeHelper.GetChild(root, i))) return true;
        return false;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApply(out string error))
        {
            StatusText.Text = error;
            return;
        }
        DialogResult = true;
    }

    private void EditSn_Click(object sender, RoutedEventArgs e)
    {
        new PhyCameraManagerWindow { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
    }
}
