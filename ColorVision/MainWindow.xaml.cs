using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.MySql;
using ColorVision.Solution;
using ColorVision.SettingUp;
using ColorVision.Template;
using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using ColorVision.Theme;
using System.Windows.Forms.Integration;
using OpenCvSharp.Flann;
using ColorVision.Video;
using ScottPlot.Drawing.Colormaps;
using ScottPlot.Styles;
using System.Drawing.Imaging;
using HandyControl.Expression.Shapes;
using Microsoft.Win32;
using log4net;
using System.Security.RightsManagement;
using ColorVision.MQTT.Control;
using ColorVision.Util;
using ColorVision.Service;
using ColorVision.MQTT.Camera;

namespace ColorVision
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        public GlobalSetting GlobalSetting { get; set; }
        public SoftwareSetting SoftwareSetting { get  {
                if (GlobalSetting.SoftwareConfig.SoftwareSetting == null)
                    GlobalSetting.SoftwareConfig.SoftwareSetting = new SoftwareSetting();
                return GlobalSetting.SoftwareConfig.SoftwareSetting;
            }
        }
        public MainWindow()
        {
            GlobalSetting = GlobalSetting.GetInstance();
            MQTTConfig mQTTConfig = GlobalSetting.SoftwareConfig.MQTTConfig;
            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            this.loader = new FlowEngineLib.STNodeLoader("FlowEngineLib.dll");

            InitializeComponent();
            this.Closed += (s, e) => {

                SoftwareSetting.Top = this.Top;
                SoftwareSetting.Left = this.Left;
                SoftwareSetting.Height = this.Height;
                SoftwareSetting.Width = this.Width;
                SoftwareSetting.WindowState = (int)this.WindowState;
            };
            if (SoftwareSetting.IsRestoreWindow && SoftwareSetting.Height != 0&& SoftwareSetting.Width != 0)
            {
                this.Top = SoftwareSetting.Top;
                this.Left = SoftwareSetting.Left;
                this.Height = SoftwareSetting.Height;
                this.Width = SoftwareSetting.Width;
                this.WindowState = (WindowState)SoftwareSetting.WindowState;
            }
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            if (WindowConfig.IsExist)
            {
                if (WindowConfig.Icon == null)
                {
                    if (!ThemeManager.AppsUseLightTheme() || !ThemeManager.SystemUsesLightTheme())
                    {
                        this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision1.ico"));
                    }
                    SystemEvents.UserPreferenceChanged += (s, e) =>
                    {
                        if (!ThemeManager.AppsUseLightTheme() || !ThemeManager.SystemUsesLightTheme())
                        {
                            this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision1.ico"));
                        }
                        else
                        {
                            this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision.ico"));
                        }
                    };
                    SystemParameters.StaticPropertyChanged += (s, e) =>
                    {
                        if (!ThemeManager.AppsUseLightTheme() || !ThemeManager.SystemUsesLightTheme())
                        {
                            this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision1.ico"));
                        }
                        else
                        {
                            this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision.ico"));
                        }
                    };
                }
                else
                {
                    this.Icon = WindowConfig.Icon;
                }
                this.Title = WindowConfig.Title ?? this.Title;
            }
            else
            {

            }

            Application.Current.MainWindow = this;

            TemplateControl = TemplateControl.GetInstance();
            await Task.Delay(30);

            StatusBarGrid.DataContext = GlobalSetting.GetInstance();
            SoftwareConfig SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
            MenuStatusBar.DataContext = SoftwareConfig;
            SiderBarGrid.DataContext = SoftwareConfig;
            if (SoftwareSetting.IsDeFaultOpenService)
            {
                new WindowService() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
            }
            else
            {

            }

            ViewGridManager = new ViewGridManager(ViewGrid);

            ViewGrid.Children.Clear();
            ViewGridManager.AddView(ImageView1);

        }

        public ViewGridManager ViewGridManager { get; set; }




        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                ImageView1.OpenImage(filePath);
            }
        }

        private void StackPanelCalibration_Initialized(object sender, EventArgs e)
        {
            ComboxCalibrationTemplate.ItemsSource = TemplateControl.GetInstance().CalibrationParams;
            ComboxCalibrationTemplate.SelectionChanged += (s, e) =>
            {
                if (ComboxCalibrationTemplate.SelectedItem is KeyValuePair<string, CalibrationParam> KeyValue && KeyValue.Value is CalibrationParam calibrationParam)
                {
                    Calibration1.CalibrationParam = calibrationParam;
                    Calibration1.DataContext = calibrationParam;
                }
            };
            ComboxCalibrationTemplate.SelectedIndex = 0;
        }


        private void MenuStatusBar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = !menuItem.IsChecked;
            }
        }
        private FlowEngineLib.STNodeLoader loader;

        private FlowControl flowControl;

        private void Button2_Click(object sender, RoutedEventArgs e)
        {

            MQTTConfig MQTTConfig = GlobalSetting.GetInstance().SoftwareConfig.MQTTConfig;
            string iPStr = MQTTConfig.Host;
            int port = MQTTConfig.Port;
            string uName = "";
            string uPwd = "";
            FlowEngineLib.MQTTHelper.SetDefaultCfg(iPStr, port, uName, uPwd, false, null);


            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.stn|*.stn";
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            loader.Load(ofd.FileName);

            flowControl = new FlowControl(MQTTControl.GetInstance(), loader.GetStartNodeName());
        }
        Window window;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            //MessageBox.Show("流程执行完成");
            window.Close();
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


        private void MenuItem11_Click(object sender, RoutedEventArgs e)
        {
            new HeartbeatWindow() { Owner = this }.Show();
        }

        private void MenuItem12_Click(object sender, RoutedEventArgs e)
        {
            new MQTTList() { Owner = this }.Show();
        }

        private void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            if (FlowTemplate.SelectedValue is FlowParam flowParam)
            {
                string startNode = loader.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    flowControl = new FlowControl(MQTTControl.GetInstance(), startNode);

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
                    flowControl.Start(sn);
                    window.Show();
                }
                else
                {
                    MessageBox.Show("流程模板为空，不能运行！！！");
                }
            }
        }

        private void StackPanelFlow_Initialized(object sender, EventArgs e)
        {
            FlowTemplate.ItemsSource = TemplateControl.GetInstance().FlowParams;

            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (FlowTemplate.SelectedValue is FlowParam flowParam)
                {
                    string fileName = GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig.GetFullFileName(flowParam.FileName?? string.Empty);
                    if (File.Exists(fileName))
                    {
                        loader.Load(fileName);
                    }
                }
            };
            FlowTemplate.SelectedIndex = 0;
        }

        private void StackPanelMQTTCameras_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                MQTTManager.DeviceSettingChanged += (s, e) =>
                {
                    stackPanel.Children.Clear();
                    foreach (var item in MQTTManager.MQTTCameras)
                    {
                        MQTTCameraControl1 Control = new MQTTCameraControl1(item);
                        item.FileHandler += (s, e) =>
                        {
                            //OpenImage(e);
                        };
                        stackPanel.Children.Add(Control);
                    }

                };
            }
        }

        private void StackPanelMQTTPGs_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                MQTTManager.DeviceSettingChanged += (s, e) =>
                {
                    stackPanel.Children.Clear();
                    foreach (var item in MQTTManager.MQTTPGs)
                    {
                        MQTTPGControl Control = new MQTTPGControl(item);
                        stackPanel.Children.Add(Control);
                    }
                };
            }
          
        }

        public MQTTManager MQTTManager { get; set; } = MQTTManager.GetInstance();
        private void StackPanelMQTTSpectrums_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                MQTTManager.DeviceSettingChanged += (s, e) =>
                {
                    stackPanel.Children.Clear();
                    foreach (var item in MQTTManager.MQTTSpectrums)
                    {
                        MQTTSpectrumControl Control = new MQTTSpectrumControl(item);
                        stackPanel.Children.Add(Control);
                    }
                };
            }
        }

        private void StackPanelMQTTVIs_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                MQTTManager.DeviceSettingChanged += (s, e) =>
                {
                    stackPanel.Children.Clear();
                    foreach (var item in MQTTManager.MQTTVISources)
                    {
                        MQTTVISourceControl Control = new MQTTVISourceControl(item);
                        stackPanel.Children.Add(Control);
                    }
                };
            }
        }

        private void MenuItem13_Click(object sender, RoutedEventArgs e)
        {
            new ServiceManagerWindow() { Owner = this }.Show();
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            ImageView1.DrawingTest();
        }

        private void Button5_Click(object sender, RoutedEventArgs e)
        {
            ViewGridManager.AddView( new ImageView());
        }

        private void Button6_Click(object sender, RoutedEventArgs e)
        {
            ViewGridManager.SetViewNum(1);
        }

        private void Button7_Click(object sender, RoutedEventArgs e)
        {
            ViewGridManager.SetViewNum(2);
        }

        private void Button8_Click(object sender, RoutedEventArgs e)
        {
            ViewGridManager.SetViewNum(4);
        }
    }


}
