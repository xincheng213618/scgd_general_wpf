using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using CVCommCore;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


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
                        ButtonSourceMeter1.Content = "打开";
                        break;
                    case DeviceStatusType.LiveOpened:
                    case DeviceStatusType.Opened:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        ButtonSourceMeter1.Content = "关闭";
                        break;
                    case DeviceStatusType.Closing:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        ButtonSourceMeter1.Content = "关闭中";
                        break;
                    case DeviceStatusType.Opening:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        ButtonSourceMeter1.Content = "打开中";
                        break;
                    default:
                        break;
                }
            }
            UpdateUI(DService.DeviceStatus);
            DService.DeviceStatusChanged += UpdateUI;


            DService.ScanResultEvent += SMUService_ScanResultHandler;
            DService.ResultEvent += SMUService_ResultHandler;

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

        private void SMUService_ResultHandler(SMUResultData data)
        {
            Config.V = data.V;
            Config.I = data.I;
        }

        private void SMUService_ScanResultHandler(SMUScanResultData data)
        {
            try
            {
                View.DrawPlot(Config.IsSourceV, Config.StopMeasureVal, data.VList, data.IList);
            }
            catch 
            {

            }
        }


        PassSxSource passSxSource = new();

        private void DoOpenByDll(Button button)
        {
            if (!passSxSource.IsOpen)
            {
                button.Content = "打开中";
                Task.Run(() =>
                {
                    if (passSxSource.Open(Config.IsNet, Config.DevName))
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            button.Content = "关闭";
                        }));
                    }
                    else
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            button.Content = "打开失败";
                        }));
                    }
                });
            }
            else
            {
                passSxSource.Close();
                button.Content = "打开";
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
            //double V = 0, I = 0;
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
