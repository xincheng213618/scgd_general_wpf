using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using ColorVision.Engine.Services.RC; // Added


namespace ColorVision.Engine.Services.Devices.SMU
{
    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySMUControl : UserControl, IDisPlayControl
    {

        public DeviceSMU Device { get; set; }
        private MQTTSMU DService { get => Device.DService;  }
        private ConfigSMU Config { get => Device.Config; }

        public ViewSMU View { get => Device.View; }

        public string DisPlayName => Device.Config.Name;

        public DisplaySMUControl(DeviceSMU deviceSMU)
        {
            Device = deviceSMU;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;

            // When switching between voltage and current modes, swap the source and limit values so that the numbers follow the semantic meaning instead of the textbox position
            if (Config is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(ConfigSMU.IsSourceV))
                    {
                        // Swap MeasureVal (source) and LmtVal (limit)
                        double oldMeasure = Config.MeasureVal;
                        Config.MeasureVal = Config.LmtVal;
                        Config.LmtVal = oldMeasure;
                    }
                };
            }

            this.ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Property, Command = Device.PropertyCommand });

            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; };
                void HideAllButtons()
                {
                    SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                    SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                    SetVisibility(StackPanelContent, Visibility.Collapsed);
                    SetVisibility(TextBlockOffLine, Visibility.Collapsed);
                }
                // Default state
                HideAllButtons();

                switch (status)
                {
                    case DeviceStatusType.Unauthorized:
                        SetVisibility(ButtonUnauthorized, Visibility.Visible);
                        break;
                    case DeviceStatusType.Unknown:
                        SetVisibility(TextBlockUnknow, Visibility.Visible);
                        break;
                    case DeviceStatusType.OffLine:
                        SetVisibility(TextBlockOffLine, Visibility.Visible);
                        break;
                    case DeviceStatusType.UnInit:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        break;
                    case DeviceStatusType.Closed:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        ButtonSourceMeter1.Content = ColorVision.Engine.Properties.Resources.Open;
                        break;
                    case DeviceStatusType.LiveOpened:
                    case DeviceStatusType.Opened:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        ButtonSourceMeter1.Content = ColorVision.Engine.Properties.Resources.Close;
                        break;
                    case DeviceStatusType.Closing:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        ButtonSourceMeter1.Content = ColorVision.Engine.Properties.Resources.Closing;
                        break;
                    case DeviceStatusType.Opening:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        ButtonSourceMeter1.Content = ColorVision.Engine.Properties.Resources.Opening;
                        break;
                    default:
                        break;
                }
            }
            UpdateUI(DService.DeviceStatus);
            DService.DeviceStatusChanged += UpdateUI;

            ComboxVITemplate.ItemsSource = TemplateSMUParam.Params;
            ComboxVITemplate.SelectionChanged += (s, e) =>
            {
                if (ComboxVITemplate.SelectedItem is TemplateModel<SMUParam> KeyValue && KeyValue.Value is SMUParam SxParm)
                {
                    Config.StartMeasureVal = SxParm.StartMeasureVal;
                    Config.StopMeasureVal = SxParm.StopMeasureVal;
                    Config.IsSourceV = SxParm.IsSourceV;
                    Config.LimitVal = SxParm.LmtVal;
                    Config.Number = SxParm.Number;
                }
            };
            ComboxVITemplate.SelectedIndex = 0;

            this.AddViewConfig(View, ComboxView);
            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }




        PassSxSource passSxSource = new();

        private void DoOpenByDll(Button button)
        {
            if (!passSxSource.IsOpen)
            {
                button.Content = ColorVision.Engine.Properties.Resources.Opening;
                Task.Run(() =>
                {
                    if (passSxSource.Open(Config.IsNet, Config.DevName))
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            button.Content = ColorVision.Engine.Properties.Resources.Close;
                        }));
                    }
                    else
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            button.Content = ColorVision.Engine.Properties.Resources.OpenFailed;
                        }));
                    }
                });
            }
            else
            {
                passSxSource.Close();
                button.Content = ColorVision.Engine.Properties.Resources.Open;
            }
        }




        private void ButtonSourceMeter1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (DService.DeviceStatus != DeviceStatusType.Opened)
                {
                    ServicesHelper.SendCommand(button, DService.Open(Config.IsNet, Config.DevName));
                }
                else
                {
                    ServicesHelper.SendCommand(button, DService.Close());
                }
            }
        }

        private void MeasureData_Click(object sender, RoutedEventArgs e)
        {
            DService.GetData(Config.IsSourceV, Config.MeasureVal, Config.LmtVal);
        }
        private void StepMeasureData_Click(object sender, RoutedEventArgs e)
        {
            DService.GetData(Config.IsSourceV, Config.MeasureVal, Config.LmtVal);
        }
        private void MeasureDataClose_Click(object sender, RoutedEventArgs e)
        {
            DService.CloseOutput();
            Config.V = null;
            Config.I = null;
        }
        private void VIScan_Click(object sender, RoutedEventArgs e)
        {
            DService.Scan(Config.IsSourceV, Config.StartMeasureVal, Config.StopMeasureVal, Config.LimitVal, Config.Number);
        }


        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                TemplateEditorWindow windowTemplate;
                if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
                {
                    MessageBox1.Show(Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                    return;
                }
                switch (control.Tag?.ToString() ?? string.Empty)
                {

                    case "SMUParam":
                        windowTemplate = new TemplateEditorWindow(new TemplateSMUParam());
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                }
            }
        }
    }
}
