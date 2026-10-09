using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Themes;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public partial class EditSMU : Window
    {
        private readonly ConfigSMU target;
        private readonly DisplaySMUConfig displayTarget;
        public SmuConfigurationDraft Draft { get; }
        public ConfigSMU EditConfig => Draft.Config;

        public EditSMU(ConfigSMU config, DisplaySMUConfig displayConfig)
        {
            target = config;
            displayTarget = displayConfig;
            Draft = new SmuConfigurationDraft(config, displayConfig);
            InitializeComponent();
            this.ApplyCaption();
            DataContext = this;
            AddFields(DeviceFields, EditConfig, nameof(ConfigSMU.Name), nameof(ConfigSMU.SN), nameof(ConfigSMU.DevType), nameof(ConfigSMU.IsAutoStart));
            AddFields(LocalFields, Draft.DisplayConfig, nameof(DisplaySMUConfig.UseLocalSmu));
            AddFields(SerialFields, Draft, nameof(SmuConfigurationDraft.SerialPortName));
            AddFields(BaudRateFields, Draft.DisplayConfig, nameof(DisplaySMUConfig.LocalBaudRate));
            AddFields(MeasurementFields, EditConfig, nameof(ConfigSMU.Is4Wire), nameof(ConfigSMU.IsFront), nameof(ConfigSMU.IsSrcA), nameof(ConfigSMU.DelayTime));
            AddFields(DisplayFields, Draft.DisplayConfig, nameof(DisplaySMUConfig.IsUseLimitSigned), nameof(DisplaySMUConfig.IsSourceV), nameof(DisplaySMUConfig.Channel));
        }

        private static void AddFields(Panel panel, object config, params string[] names)
        {
            foreach (string name in names)
            {
                var field = PropertyEditorHelper.GenProperties(config, name, Properties.Resources.ResourceManager);
                field.Margin = new Thickness(0, 3, 0, 3);
                field.MinHeight = 28;
                if (panel.Name is "MeasurementFields" or "DisplayFields")
                    foreach (UIElement child in field.Children)
                        if (child is TextBox or ComboBox) ((Control)child).MinWidth = 100;
                panel.Children.Add(field);
            }
        }

        private void IpEdit_Click(object sender, RoutedEventArgs e)
        {
            SetIpEditing(IpAddressBox.IsReadOnly);
        }

        private void SetIpEditing(bool editing)
        {
            IpAddressBox.IsReadOnly = !editing;
            IpEditButton.Content = editing ? Properties.Resources.OK : Properties.Resources.Edit;
            if (editing)
            {
                IpAddressBox.FocusFirstSegment();
            }
        }

        private void IpAddress_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !IpAddressBox.IsReadOnly)
            {
                SetIpEditing(false);
                e.Handled = true;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            if (!Draft.TryApply(target, displayTarget, out string error))
            {
                StatusText.Text = error;
                if (Draft.IsNet) SetIpEditing(true);
                return;
            }
            DialogResult = true;
        }
    }
}
