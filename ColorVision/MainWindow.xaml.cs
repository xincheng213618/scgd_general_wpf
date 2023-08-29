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
        private FlowView flowView;
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
            GlobalSetting = GlobalSetting.GetInstance();

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
            MQTTConfig mQTTConfig = GlobalSetting.SoftwareConfig.MQTTConfig;
            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowView = new FlowView();
            ViewGridManager.GetInstance().AddView(flowView, ComboxView, flowView.View);


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
            ViewGrid.Children.Clear();
            ViewGridManager.AddView(ImageView1);

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

        private void ButtonCV_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.custom) | *.custom";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                if (ViewGridManager.CurrentView is ImageView imageView)
                {
                    imageView.OpenCVImage(filePath);
                }
                else
                {
                    ImageView1.OpenCVImage(filePath);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                if (ViewGridManager.CurrentView is ImageView imageView)
                {
                    imageView.OpenImage(filePath);
                }
                else
                {
                    ImageView1.OpenImage(filePath);
                }
            }
        }


        private void MenuStatusBar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = !menuItem.IsChecked;
            }
        }
       // private FlowEngineLib.STNodeLoader loader;

        private FlowControl flowControl;
        Window window;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            //MessageBox.Show("流程执行完成");
            window.Close();

            if (sender!=null)
            {
                FlowControlData flowControlData = (FlowControlData)sender;
                ServiceControl.GetInstance().SpectrumDrawPlotFromDB(flowControlData.SerialNumber);
            }
        }
        bool CameraOpen;

        private void Button4_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                CameraVideoControl control = CameraVideoControl.GetInstance();
                if (!CameraOpen)
                {
                    button.Content = "正在获取推流";
                    control.Open();
                    control.CameraVideoFrameReceived += (bmp) =>
                    {
                        button.Content = "关闭视频";
                        if (ImageView1.ImageShow.Source is WriteableBitmap bitmap)
                        {
                            ImageUtil.BitmapCopyToWriteableBitmap(bmp, bitmap, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.PixelFormat);
                        }
                        else
                        {
                            WriteableBitmap writeableBitmap = ImageUtil.BitmapToWriteableBitmap(bmp);
                            ImageView1.ImageShow.Source = writeableBitmap;
                        }
                    };
                }
                else
                {
                    button.Content = "启用视频模式";
                    control.Close();
                }
                CameraOpen = !CameraOpen;
            }
        }


        private void MenuItem12_Click(object sender, RoutedEventArgs e)
        {
            new MQTTList() { Owner = this }.Show();
        }

        private void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            if (FlowTemplate.SelectedValue is FlowParam flowParam)
            {
                string startNode = flowView.FlowEngineControl.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    flowControl = new FlowControl(MQTTControl.GetInstance(), flowView.FlowEngineControl);

                    window = new Window() { Width = 400, Height = 400, Title = "流程返回信息", Owner = this, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                    TextBox textBox = new TextBox() { IsReadOnly = true, Background = Brushes.Black, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

                    Grid grid = new Grid();
                    grid.Children.Add(textBox);

                    grid.Children.Add(new Controls.ProgressRing() { Margin = new Thickness(100, 100, 100, 100) });

                    window.Content = grid;

                    textBox.Text = "TTL:" + "0";
                    flowControl.FlowData += (s, e) =>
                    {
                        if (s is FlowControlData msg)
                        {
                            textBox.Text = "TTL:" + msg.Params.TTL.ToString();
                        }
                    };
                    flowControl.FlowCompleted += FlowControl_FlowCompleted;
                    string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                    ServiceControl.GetInstance().ResultBatchSave(sn);
                    flowControl.Start(sn);
                    window.Show();
                }
                else
                {
                    MessageBox.Show("流程模板为空，不能运行！！！");
                }
            }
        }

        private void Button_FlowStop_Click(object sender, RoutedEventArgs e)
        {
            if (flowControl != null)
            {
                flowControl.Stop();
            }
        }

        private void StackPanelFlow_Initialized(object sender, EventArgs e)
        {
            FlowTemplate.ItemsSource = TemplateControl.GetInstance().FlowParams;
            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (FlowTemplate.SelectedValue is FlowParam flowParam)
                {
                    string fileName = GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig.GetFullFileName(flowParam.FileName ?? string.Empty);
                    if (File.Exists(fileName))
                    {
                        if (flowView != null)
                        {
                            try
                            {
                                flowView.FlowEngineControl.Load(fileName);
                            }
                            catch
                            {

                            }
                        }
                    }
                }
            };
            FlowTemplate.SelectedIndex = 0;
        }


        private void MenuItem13_Click(object sender, RoutedEventArgs e)
        {
            new ServiceManagerWindow() { Owner = this }.Show();
        }

        private void Button5_Click(object sender, RoutedEventArgs e)
        {
            ViewGridManager.AddView(new ImageView());
            foreach (var item in ViewGridManager.Views)
            {
                if (item is ImageView imageView)
                {
                    imageView.Zoombox1.ZoomUniform();
                }
            }
        }

        private void Button51_Click(object sender, RoutedEventArgs e)
        {
            ViewGridManager.AddView(new SMUView());
        }


        private void StackPanelMQTT_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                stackPanel.Children.Add(ServiceControl.GetInstance().MQTTStackPanel);
            }
        }

        private void Button10_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                int v = int.Parse(button.Tag.ToString() ?? "0");
                if (v >= ViewGridManager.Views.Count)
                    return;

                ViewGridManager.SetOneView(v);

                if (ViewGridManager.Views[v] is ImageView imageView)
                {
                    imageView.Zoombox1.ZoomUniform();
                }

            }
        }


        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            SiderBarGrid.Width = SiderCol.ActualWidth;
            SiderCol.Width = GridLength.Auto;
            ViewCol.Width = new GridLength(1, GridUnitType.Star);
        }

        private void Button1_Click_1(object sender, RoutedEventArgs e)
        {
            if(sender is MenuItem button && int.TryParse(button.Tag.ToString() ,out int nums))
            {
                switch (nums)
                {
                    case 20:
                        ViewGridManager.SetViewGridTwo();
                        break;
                    case 21:
                        ViewGridManager.SetViewGrid(2);
                        break;
                    default:
                        ViewGridManager.SetViewGrid(nums);
                        break;
                }

                ViewGridManager.SetViewGrid(nums);
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

        private void MenuItm_Template(object sender, RoutedEventArgs e)
        {

        }
    }
}
