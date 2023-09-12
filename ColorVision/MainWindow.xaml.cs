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
using HandyControl.Tools.Extension;
using HandyControl.Tools;
using System.Windows.Media.Animation;
using ColorVision.Device.POI;

namespace ColorVision
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        private GridLength _columnDefinitionWidth;
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

            


            if (!WindowConfig.IsExist||(WindowConfig.IsExist&& WindowConfig.Icon == null)) {
                ThemeManager.Current.SystemThemeChanged += (e) => {
                    this.Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision;component/Image/{(e == Theme.Theme.Light ? "ColorVision.ico" : "ColorVision1.ico")}"));
                };
                if (ThemeManager.Current.SystemTheme == Theme.Theme.Dark)
                    this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision1.ico"));
            }

            if (WindowConfig.IsExist)
            {
                if (WindowConfig.Icon != null)
                    this.Icon = WindowConfig.Icon;
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
            //SiderBarGrid.DataContext = SoftwareConfig;

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
            ViewGridManager.GetInstance().SetViewNum(1);

            SPDisplay.Children.Add(new POIDisplayControl());
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
            new LoginWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void OnLeftMainContentShiftOut(object sender, RoutedEventArgs e)
        {
            ButtonShiftOut.Collapse();
            GridSplitter.IsEnabled = false;

            double targetValue = -ColumnDefinitionLeft.MaxWidth;
            _columnDefinitionWidth = ColumnDefinitionLeft.Width;

            DoubleAnimation animation = AnimationHelper.CreateAnimation(targetValue, milliseconds: 100);
            animation.FillBehavior = FillBehavior.Stop;
            animation.Completed += OnAnimationCompleted;
            LeftMainContent.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            void OnAnimationCompleted(object? _, EventArgs args)
            {
                animation.Completed -= OnAnimationCompleted;
                LeftMainContent.RenderTransform.SetCurrentValue(TranslateTransform.XProperty, targetValue);

                Grid.SetColumn(MainContent, 0);
                Grid.SetColumnSpan(MainContent, 2);

                ColumnDefinitionLeft.MinWidth = 0;
                ColumnDefinitionLeft.Width = new GridLength();
                ButtonShiftIn.Show();
            }
        }

        private void OnLeftMainContentShiftIn(object sender, RoutedEventArgs e)
        {
            ButtonShiftIn.Collapse();
            GridSplitter.IsEnabled = true;

            double targetValue = ColumnDefinitionLeft.Width.Value;

            DoubleAnimation animation = AnimationHelper.CreateAnimation(targetValue, milliseconds: 100);
            animation.FillBehavior = FillBehavior.Stop;
            animation.Completed += OnAnimationCompleted;
            LeftMainContent.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            void OnAnimationCompleted(object? _, EventArgs args)
            {
                animation.Completed -= OnAnimationCompleted;
                LeftMainContent.RenderTransform.SetCurrentValue(TranslateTransform.XProperty, targetValue);

                Grid.SetColumn(MainContent, 1);
                Grid.SetColumnSpan(MainContent, 1);

                ColumnDefinitionLeft.MinWidth = 240;
                ColumnDefinitionLeft.Width = _columnDefinitionWidth;
                ButtonShiftOut.Show();
            }
        }
    }
}
