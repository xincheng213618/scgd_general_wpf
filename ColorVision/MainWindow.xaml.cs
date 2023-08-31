using ColorVision.MQTT;
using ColorVision.SettingUp;
using ColorVision.Template;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorVision.Theme;
using ColorVision.Util;
using ColorVision.Service;
using ColorVision.MQTT.Service;
using ColorVision.Device.SMU;
using ColorVision.Device.Camera.Video;
using ColorVision.Flow;

namespace ColorVision
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        public ViewGridManager ViewGridManager { get; set; }

        public GlobalSetting GlobalSetting { get; set; }
        public SoftwareSetting SoftwareSetting
        {
            get
            {
                if (GlobalSetting.SoftwareConfig.SoftwareSetting == null)
                    GlobalSetting.SoftwareConfig.SoftwareSetting = new SoftwareSetting();
                return GlobalSetting.SoftwareConfig.SoftwareSetting;
            }
        }
        public MainWindow()
        {

            InitializeComponent();
            if (SoftwareSetting.IsRestoreWindow && SoftwareSetting.Height != 0 && SoftwareSetting.Width != 0)
            {
                this.Top = SoftwareSetting.Top;
                this.Left = SoftwareSetting.Left;
                this.Height = SoftwareSetting.Height;
                this.Width = SoftwareSetting.Width;
                this.WindowState = (WindowState)SoftwareSetting.WindowState;
            }
            this.Closed += (s, e) =>
            {

                SoftwareSetting.Top = this.Top;
                SoftwareSetting.Left = this.Left;
                SoftwareSetting.Height = this.Height;
                SoftwareSetting.Width = this.Width;
                SoftwareSetting.WindowState = (int)this.WindowState;
            };
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            GlobalSetting = GlobalSetting.GetInstance();
            FlowDisplayControl flowDisplayControl = new FlowDisplayControl();
            SPDisplay.Children.Insert(0, flowDisplayControl);


            if (WindowConfig.IsExist)
            {
                if (WindowConfig.Icon == null)
                {
                    ThemeManager.Current.SystemThemeChanged += (e) => {
                        this.Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision;component/Image/{(e == Theme.Theme.Dark? "ColorVision.ico":"ColorVision1.ico")}"));
                    };
                    if (ThemeManager.Current.SystemTheme == Theme.Theme.Dark)
                        this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision1.ico"));
                }
                else
                {
                    this.Icon = WindowConfig.Icon;
                }
                this.Title = WindowConfig.Title ?? this.Title;
            }

            Application.Current.MainWindow = this;
            TemplateControl = TemplateControl.GetInstance();
            ViewGridManager = ViewGridManager.GetInstance();
            ViewGridManager.MainView = ViewGrid;

            await Task.Delay(30);
            StatusBarGrid.DataContext = GlobalSetting.GetInstance();
            SoftwareConfig SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
            MenuStatusBar.DataContext = SoftwareConfig;
            SiderBarGrid.DataContext = SoftwareConfig;

            try
            {
                if (!SoftwareSetting.IsDeFaultOpenService)
                {
                    new WindowDevices() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
                }
                else
                {
                    ServiceControl.GetInstance().GenContorl();
                }
            }
            catch
            {
                MessageBox.Show("窗口创建错误");
                Environment.Exit(-1);
            }
            ViewGridManager.GetInstance().SetViewNum(-1);
        }
        private void MenuStatusBar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = !menuItem.IsChecked;
            }
        }

        private void MenuItem12_Click(object sender, RoutedEventArgs e)
        {
            new MQTTList() { Owner = this }.Show();
        }

        private void StackPanelMQTT_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                stackPanel.Children.Add(ServiceControl.GetInstance().MQTTStackPanel);
            }
        }



        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            SiderBarGrid.Width = SiderCol.ActualWidth;
            SiderCol.Width = GridLength.Auto;
            ViewCol.Width = new GridLength(1, GridUnitType.Star);
        }
        private void ViewGrid_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag.ToString(), out int nums))
            {
                switch (nums)
                {
                    case 20:
                        ViewGridManager.SetViewGridTwo();
                        break;
                    case 21:
                        ViewGridManager.SetViewGrid(2);
                        break;
                    case 30:
                        ViewGridManager.SetViewGridThree();
                        break;
                    case 31:
                        ViewGridManager.SetViewGridThree(false);
                        break;
                    default:
                        ViewGridManager.SetViewGrid(nums);
                        break;
                }
            }
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.ShowDialog();
        }
    }
}
