using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Dao;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes.Controls;
using ColorVision.UI.LogImp;
using ColorVision.UI.Menus;
using log4net;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;

namespace ProjectStarkSemi
{
    /// <summary>
    /// 硬件型号枚举
    /// </summary>
    public enum ConoscopeModelType
    {
        /// <summary>
        /// VA60: 一台观察相机（视频模式）+ 一台测量相机（需要校正）
        /// </summary>
        VA60,
        
        /// <summary>
        /// VA80: 一台测量相机（需要校正）
        /// </summary>
        VA80
    }

    /// <summary>
    /// 图像滤波类型枚举
    /// </summary>
    public enum ImageFilterType
    {
        /// <summary>
        /// 无滤波
        /// </summary>
        None,
        
        /// <summary>
        /// 低通滤波（均值滤波）
        /// </summary>
        LowPass,
        
        /// <summary>
        /// 移动平均滤波（方框滤波）
        /// </summary>
        MovingAverage,
        
        /// <summary>
        /// 高斯滤波
        /// </summary>
        Gaussian,
        
        /// <summary>
        /// 中值滤波
        /// </summary>
        Median,
        
        /// <summary>
        /// 双边滤波
        /// </summary>
        Bilateral
    }

    /// <summary>
    /// 极角线数据类，存储角度、线对象和RGB数据
    /// </summary>
    public class PolarAngleLine
    {
        /// <summary>
        /// 极角（度）
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// 绘制的线对象
        /// </summary>
        public DVLine? Line { get; set; }

        /// <summary>
        /// 沿线采样的RGB数据
        /// </summary>
        public List<RgbSample> RgbData { get; set; } = new List<RgbSample>();

        /// <summary>
        /// 是否显示此线的数据
        /// </summary>
        public bool IsVisible { get; set; } = true;

        public override string ToString() => $"{Angle}°";
    }

    /// <summary>
    /// RGB采样点数据
    /// </summary>
    public class RgbSample
    {
        /// <summary>
        /// 位置（从-80到80映射）
        /// </summary>
        public double Position { get; set; }

        /// <summary>
        /// 红色通道值
        /// </summary>
        public double R { get; set; }

        /// <summary>
        /// 绿色通道值
        /// </summary>
        public double G { get; set; }

        /// <summary>
        /// 蓝色通道值
        /// </summary>
        public double B { get; set; }
    }

    public class MenuConoscopeWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 50;
        public override string Header => "Conoscope";

        public override void Execute()
        {
            ConoscopeWindow conoscopeWindow = new ConoscopeWindow();
            conoscopeWindow.Show();
        }
    }

    /// <summary>
    /// ConoscopeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ConoscopeWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeWindow));

        private ConoscopeModelType currentModel = ConoscopeModelType.VA60;
        private MVSViewWindow? observationCameraWindow;
        private LogOutput? logOutput;

        private DeviceCamera? Device;

        // Polar angle line management
        private ObservableCollection<PolarAngleLine> polarAngleLines = new ObservableCollection<PolarAngleLine>();
        private PolarAngleLine? selectedPolarLine;
        
        // RGB channel visibility flags
        private bool showRedChannel = true;
        private bool showGreenChannel = true;
        private bool showBlueChannel = true;

        public ConoscopeWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            // Initialize LogOutput control
            logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
            LogGrid.Children.Add(logOutput);

            // Load available camera services
            LoadCameraServices();

            // Initialize UI
            UpdateUIForModel(currentModel);

            wpfPlot.Plot.Title($"视角分布曲线");
            wpfPlot.Plot.XLabel("Degress");
            wpfPlot.Plot.YLabel("Luminance (cd/m²)");
            wpfPlot.Plot.Legend.FontName = ScottPlot.Fonts.Detect("中文");

            // Set font for labels to support international characters
            // Use a consistent string for font detection
            string fontSample = $"中文 Luminance Voltage";
            wpfPlot.Plot.Axes.Title.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            wpfPlot.Plot.Axes.Left.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            wpfPlot.Plot.Axes.Bottom.Label.FontName = ScottPlot.Fonts.Detect(fontSample);

            // Enable grid for better readability
            wpfPlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
            wpfPlot.Plot.Grid.MajorLineWidth = 1;
            wpfPlot.Plot.Axes.SetLimits(-80, 80, 0, 600);

            wpfPlot.Refresh();

            this.Closed += (s, e) =>
            {
                this.Dispose();
            };
        }

        private void LoadCameraServices()
        {
            var cameras = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList();
            
            cbMeasurementCamera.ItemsSource = cameras;
            cbMeasurementCamera.DisplayMemberPath = "Name";
            if (cameras.Count > 0)
                cbMeasurementCamera.SelectedIndex = 0;
        }


        private void cbMeasurementCamera_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Device = cbMeasurementCamera.SelectedItem as DeviceCamera;
            if (Device != null)
            {
                log.Info($"已选择测量相机: {Device.Name}");
                // Load calibration templates for selected camera
                ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams.CreateEmpty();
                ComboxCalibrationTemplate.SelectedIndex = 0;
            }
        }


        private void cbModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbModelType.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string modelTag)
            {
                if (Enum.TryParse<ConoscopeModelType>(modelTag, out var modelType))
                {
                    currentModel = modelType;
                    UpdateUIForModel(currentModel);
                    log.Info($"已切换到型号: {currentModel}");
                }
            }
        }

        private void UpdateUIForModel(ConoscopeModelType model)
        {
            if (tbCurrentModel == null) return;
            
            tbCurrentModel.Text = model.ToString();

            switch (model)
            {
                case ConoscopeModelType.VA60:
                    // VA60: 显示观察相机控件和状态栏项
                    btnOpenObservationCamera.Visibility = Visibility.Visible;
                    if (ObservationCameraStatusItem != null)
                    {
                        ObservationCameraStatusItem.Visibility = Visibility.Visible;
                    }
                    if (ObservationCameraSeparator != null)
                    {
                        ObservationCameraSeparator.Visibility = Visibility.Visible;
                    }
                    break;
                    
                case ConoscopeModelType.VA80:
                    // VA80: 隐藏观察相机控件和状态栏项
                    btnOpenObservationCamera.Visibility = Visibility.Collapsed;
                    if (ObservationCameraStatusItem != null)
                    {
                        ObservationCameraStatusItem.Visibility = Visibility.Collapsed;
                    }
                    if (ObservationCameraSeparator != null)
                    {
                        ObservationCameraSeparator.Visibility = Visibility.Collapsed;
                    }
                    break;
            }
        }

        private void btnOpenObservationCamera_Click(object sender, RoutedEventArgs e)
        {
            observationCameraWindow = new MVSViewWindow();
            observationCameraWindow.Show();
        }

        private void btnOpenMeasurementCamera_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Device == null)
                {
                    MessageBox.Show("请先选择测量相机", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }


                if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                {

                }
                else
                {
                    param = new CalibrationParam() { Id = -1, Name = "Empty" };
                }

                log.Info($"准备获取图像 - 相机: {Device.Name}, 校正: {param.Name}");
                
                double[] expTime = new double[] { Device.Config.ExpTime };
                AutoExpTimeParam autoExpTimeParam = new AutoExpTimeParam { Id = -1 };
                ParamBase hdrParam = new ParamBase { Id = -1 };

                MsgRecord msgRecord = Device.DService.GetData(expTime, param, autoExpTimeParam, hdrParam);

                if (msgRecord != null)
                {
                    tbMeasurementCameraStatus.Text = "正在获取...";
                    tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Orange);
                    msgRecord.MsgSucessed += (arg) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            int masterId = Convert.ToInt32(arg.Data.MasterId);
                            List<MeasureResultImgModel> resultMaster = null;
                            
                            if (masterId > 0)
                            {
                                resultMaster = new List<MeasureResultImgModel>();
                                MeasureResultImgModel model = MeasureImgResultDao.Instance.GetById(masterId);
                                if (model != null)
                                    resultMaster.Add(model);
                            }
                            
                            if (resultMaster != null && resultMaster.Count > 0)
                            {
                                foreach (MeasureResultImgModel result in resultMaster)
                                {
                                    try
                                    {
                                        if (result.FileUrl != null)
                                        {
                                            ImageView.OpenImage(result.FileUrl);
                                            tbMeasurementCameraStatus.Text = "已获取";
                                            tbMeasurementCameraStatus.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Green);
                                            log.Info($"成功加载图像: {result.FileUrl}");
                                        }
                                        else
                                        {
                                            tbMeasurementCameraStatus.Text = "失败";
                                            tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Red);
                                            MessageBox.Show("获取图像失败，找不到文件地址", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                            log.Error("获取图像失败：找不到文件地址");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tbMeasurementCameraStatus.Text = "失败";
                                        tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Red);
                                        MessageBox.Show($"打开图像失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                        log.Error($"打开图像失败: {ex.Message}", ex);
                                    }
                                }
                            }
                            else
                            {
                                tbMeasurementCameraStatus.Text = "无数据";
                                tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Red);
                                log.Warn("未获取到图像数据");
                            }
                        });
                    };
                }
                else
                {
                    tbMeasurementCameraStatus.Text = "失败";
                    tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Red);
                    MessageBox.Show("发送命令失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    log.Error("发送GetData命令失败");
                }
            }
            catch (Exception ex)
            {
                tbMeasurementCameraStatus.Text = "异常";
                tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Red);
                log.Error($"打开测量相机失败: {ex.Message}", ex);
                MessageBox.Show($"打开测量相机失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCalibration_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for calibration functionality
            // This will be implemented in future iterations
            log.Info("校正功能将在后续版本中实现");
            MessageBox.Show("校正功能将在后续版本中实现。\n\n这里将实现测量相机的常规校正流程。", 
                "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void cbFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbFilterType == null) return;
            
            var selectedFilter = (ImageFilterType)cbFilterType.SelectedIndex;
            
            // Update parameter visibility based on filter type
            if (sliderKernelSize != null && sliderSigma != null && sliderD != null && sliderSigmaColor != null && sliderSigmaSpace != null)
            {
                switch (selectedFilter)
                {
                    case ImageFilterType.None:
                        sliderKernelSize.IsEnabled = false;
                        sliderSigma.IsEnabled = false;
                        sliderD.IsEnabled = false;
                        sliderSigmaColor.IsEnabled = false;
                        sliderSigmaSpace.IsEnabled = false;
                        break;
                    case ImageFilterType.LowPass:
                    case ImageFilterType.MovingAverage:
                    case ImageFilterType.Median:
                        sliderKernelSize.IsEnabled = true;
                        sliderSigma.IsEnabled = false;
                        sliderD.IsEnabled = false;
                        sliderSigmaColor.IsEnabled = false;
                        sliderSigmaSpace.IsEnabled = false;
                        break;
                    case ImageFilterType.Gaussian:
                        sliderKernelSize.IsEnabled = true;
                        sliderSigma.IsEnabled = true;
                        sliderD.IsEnabled = false;
                        sliderSigmaColor.IsEnabled = false;
                        sliderSigmaSpace.IsEnabled = false;
                        break;
                    case ImageFilterType.Bilateral:
                        sliderKernelSize.IsEnabled = false;
                        sliderSigma.IsEnabled = false;
                        sliderD.IsEnabled = true;
                        sliderSigmaColor.IsEnabled = true;
                        sliderSigmaSpace.IsEnabled = true;
                        break;
                }
            }
        }

        private void btnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ImageView.ImageShow.Source == null)
                {
                    MessageBox.Show("请先获取图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var filterType = (ImageFilterType)cbFilterType.SelectedIndex;
                if (filterType == ImageFilterType.None)
                {
                    log.Info("未选择滤波类型");
                    return;
                }

                log.Info($"开始应用滤波: {filterType}");

                // Convert WPF BitmapSource to OpenCV Mat
                BitmapSource bitmapSource = ImageView.ImageShow.Source as BitmapSource;
                if (bitmapSource == null)
                {
                    MessageBox.Show("图像格式不支持", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                OpenCvSharp.Mat srcMat = BitmapSourceConverter.ToMat(bitmapSource);
                OpenCvSharp.Mat dstMat = new OpenCvSharp.Mat();

                int kernelSize = (int)sliderKernelSize.Value;
                double sigma = sliderSigma.Value;
                int d = (int)sliderD.Value;
                double sigmaColor = sliderSigmaColor.Value;
                double sigmaSpace = sliderSigmaSpace.Value;

                // Ensure kernel size is odd
                if (kernelSize % 2 == 0) kernelSize++;

                // Apply selected filter
                switch (filterType)
                {
                    case ImageFilterType.LowPass:
                        // 低通滤波（均值滤波）
                        OpenCvSharp.Cv2.Blur(srcMat, dstMat, new OpenCvSharp.Size(kernelSize, kernelSize));
                        log.Info($"应用低通滤波，核大小: {kernelSize}");
                        break;

                    case ImageFilterType.MovingAverage:
                        // 移动平均滤波（方框滤波）
                        OpenCvSharp.Cv2.BoxFilter(srcMat, dstMat, srcMat.Type(), new OpenCvSharp.Size(kernelSize, kernelSize));
                        log.Info($"应用移动平均滤波，核大小: {kernelSize}");
                        break;

                    case ImageFilterType.Gaussian:
                        // 高斯滤波
                        OpenCvSharp.Cv2.GaussianBlur(srcMat, dstMat, new OpenCvSharp.Size(kernelSize, kernelSize), sigma);
                        log.Info($"应用高斯滤波，核大小: {kernelSize}, σ: {sigma}");
                        break;

                    case ImageFilterType.Median:
                        // 中值滤波
                        OpenCvSharp.Cv2.MedianBlur(srcMat, dstMat, kernelSize);
                        log.Info($"应用中值滤波，核大小: {kernelSize}");
                        break;

                    case ImageFilterType.Bilateral:
                        // 双边滤波
                        OpenCvSharp.Cv2.BilateralFilter(srcMat, dstMat, d, sigmaColor, sigmaSpace);
                        log.Info($"应用双边滤波，d: {d}, σColor: {sigmaColor}, σSpace: {sigmaSpace}");
                        break;
                }

                // Convert back to WPF BitmapSource
                BitmapSource filteredImage = BitmapSourceConverter.ToBitmapSource(dstMat);
                ImageView.SetImageSource(filteredImage);

                srcMat.Dispose();
                dstMat.Dispose();

                log.Info("滤波应用成功");
                MessageBox.Show("滤波应用成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"应用滤波失败: {ex.Message}", ex);
                MessageBox.Show($"应用滤波失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Dispose()
        {
            logOutput?.Dispose();
            ImageView?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (Device.PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "在使用校正前，请先配置对映的物理相机", "ColorVision");
                return;
            }

            var ITemplate = new TemplateCalibrationParam(Device.PhyCamera);
            var windowTemplate = new TemplateEditorWindow(ITemplate, ComboxCalibrationTemplate.SelectedIndex - 1) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();

            ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams.CreateEmpty();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageView.OpenImage(openFileDialog.FileName);
                ImageView.ImageShow.ImageInitialized += (s, e) =>
                {
                    CreateAndAnalyzePolarLines();

                };
            }
        }

        /// <summary>
        /// 创建极角线并进行分析
        /// </summary>
        private void CreateAndAnalyzePolarLines()
        {
            try
            {
                // Check if image is loaded
                if (ImageView.ImageShow.Source == null)
                {
                    log.Warn("图像未加载，无法创建极角线");
                    return;
                }

                BitmapSource bitmapSource = ImageView.ImageShow.Source as BitmapSource;
                if (bitmapSource == null)
                {
                    log.Error("无法获取图像源");
                    return;
                }

                // Get image dimensions
                int imageWidth = bitmapSource.PixelWidth;
                int imageHeight = bitmapSource.PixelHeight;
                
                // Use the smaller dimension for circular symmetry
                int radius = Math.Min(imageWidth, imageHeight) / 2;
                
                // Calculate center point
                Point center = new Point(imageWidth / 2.0, imageHeight / 2.0);

                log.Info($"图像尺寸: {imageWidth}x{imageHeight}, 中心: ({center.X}, {center.Y}), 半径: {radius}");

                // Clear existing lines
                ClearPolarLines();

                // Default angles: 0, 20, 40, 90
                double[] defaultAngles = { 0, 20, 40, 90,110,130 };

                // Create lines for each angle
                foreach (double angle in defaultAngles)
                {
                    CreatePolarLine(angle, center, radius, bitmapSource);
                }

                // Select the first line by default
                if (polarAngleLines.Count > 0)
                {
                    selectedPolarLine = polarAngleLines[0];
                    cbPolarAngleLines.ItemsSource = polarAngleLines;
                    cbPolarAngleLines.SelectedIndex = 0;
                    UpdatePlot();
                }

                log.Info($"成功创建 {polarAngleLines.Count} 条极角线");
            }
            catch (Exception ex)
            {
                log.Error($"创建极角线失败: {ex.Message}", ex);
                MessageBox.Show($"创建极角线失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 极角线选择改变事件
        /// </summary>
        private void cbPolarAngleLines_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbPolarAngleLines.SelectedItem is PolarAngleLine selectedLine)
            {
                selectedPolarLine = selectedLine;
                UpdatePlot();
            }
        }

        /// <summary>
        /// RGB通道可见性改变事件
        /// </summary>
        private void RgbChannelVisibility_Changed(object sender, RoutedEventArgs e)
        {
            if (chkShowRed == null || chkShowGreen ==null || chkShowBlue ==null) return;
            showRedChannel = chkShowRed.IsChecked ?? true;
            showGreenChannel = chkShowGreen.IsChecked ?? true;
            showBlueChannel = chkShowBlue.IsChecked ?? true;
            UpdatePlot();
        }

        /// <summary>
        /// 创建指定角度的极角线
        /// </summary>
        private void CreatePolarLine(double angle, System.Windows.Point center, int radius, BitmapSource bitmapSource)
        {
            // Convert angle to radians
            double radians = angle * Math.PI / 180.0;

            // Calculate line endpoints
            double dx = radius * Math.Cos(radians);
            double dy = radius * Math.Sin(radians);

            Point start = new Point(center.X - dx, center.Y - dy);
            Point end = new Point(center.X + dx, center.Y + dy);

            // Create DVLine
            DVLine line = new DVLine();
            line.Points.Add(start);
            line.Points.Add(end);
            line.Pen = new Pen(Brushes.Yellow, 2);
            line.Render();

            // Add to ImageView
            ImageView.AddVisual(line);

            // Create PolarAngleLine object
            PolarAngleLine polarLine = new PolarAngleLine
            {
                Angle = angle,
                Line = line
            };

            // Extract RGB data along the line
            ExtractRgbAlongLine(polarLine, start, end, bitmapSource, radius);

            // Add to collection
            polarAngleLines.Add(polarLine);

            log.Info($"创建极角线: {angle}°, 起点({start.X:F1}, {start.Y:F1}), 终点({end.X:F1}, {end.Y:F1})");
        }

        /// <summary>
        /// 沿线提取RGB数据
        /// </summary>
        private void ExtractRgbAlongLine(PolarAngleLine polarLine, Point start, Point end, BitmapSource bitmapSource, int radius)
        {
            try
            {
                // Convert BitmapSource to OpenCV Mat
                OpenCvSharp.Mat mat = BitmapSourceConverter.ToMat(bitmapSource);

                // Calculate line length
                double lineLength = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
                int numSamples = (int)lineLength;

                if (numSamples <= 1)
                {
                    log.Warn($"线长度太短 ({numSamples} 像素)，无法采样");
                    mat.Dispose();
                    return;
                }

                // Sample points along the line
                for (int i = 0; i < numSamples; i++)
                {
                    double t = i / (double)(numSamples - 1);
                    double x = start.X + t * (end.X - start.X);
                    double y = start.Y + t * (end.Y - start.Y);

                    // Ensure coordinates are within bounds
                    int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                    int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                    // Map position from pixel index to -80 to 80 range
                    // Linear mapping: position = -80 + (i / (numSamples - 1)) * 160
                    // This ensures: i=0 -> -80°, i=(numSamples-1) -> 80°
                    double position = -80 + (i / (double)(numSamples - 1)) * 160;

                    // Extract RGB values based on image type
                    double r = 0, g = 0, b = 0;

                    if (mat.Channels() == 1)
                    {
                        // Grayscale image
                        if (mat.Depth() == OpenCvSharp.MatType.CV_8U)
                        {
                            byte value = mat.At<byte>(iy, ix);
                            r = g = b = value;
                        }
                        else if (mat.Depth() == OpenCvSharp.MatType.CV_16U)
                        {
                            ushort value = mat.At<ushort>(iy, ix);
                            r = g = b = value;
                        }
                    }
                    else if (mat.Channels() >= 3)
                    {
                        // Color image (BGR or BGRA)
                        if (mat.Depth() == OpenCvSharp.MatType.CV_8U)
                        {
                            OpenCvSharp.Vec3b pixel = mat.At<OpenCvSharp.Vec3b>(iy, ix);
                            b = pixel.Item0;
                            g = pixel.Item1;
                            r = pixel.Item2;
                        }
                        else if (mat.Depth() == OpenCvSharp.MatType.CV_16U)
                        {
                            OpenCvSharp.Vec3w pixel = mat.At<OpenCvSharp.Vec3w>(iy, ix);
                            b = pixel.Item0;
                            g = pixel.Item1;
                            r = pixel.Item2;
                        }
                    }

                    polarLine.RgbData.Add(new RgbSample
                    {
                        Position = position,
                        R = r,
                        G = g,
                        B = b
                    });
                }

                mat.Dispose();
                log.Info($"完成RGB采样: 角度{polarLine.Angle}°, 采样点数{polarLine.RgbData.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取RGB数据失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 清除所有极角线
        /// </summary>
        private void ClearPolarLines()
        {
            foreach (var polarLine in polarAngleLines)
            {
                if (polarLine.Line != null)
                {
                    ImageView.DrawingVisualLists.Remove(polarLine.Line);
                }
            }
            polarAngleLines.Clear();
            selectedPolarLine = null;
        }

        /// <summary>
        /// 更新ScottPlot显示
        /// </summary>
        private void UpdatePlot()
        {
            try
            {
                wpfPlot.Plot.Clear();

                if (selectedPolarLine == null || selectedPolarLine.RgbData.Count == 0)
                {
                    wpfPlot.Refresh();
                    return;
                }

                // Extract position and RGB data
                double[] positions = selectedPolarLine.RgbData.Select(s => s.Position).ToArray();
                double[] rValues = selectedPolarLine.RgbData.Select(s => s.R).ToArray();
                double[] gValues = selectedPolarLine.RgbData.Select(s => s.G).ToArray();
                double[] bValues = selectedPolarLine.RgbData.Select(s => s.B).ToArray();

                // Add scatter plots for each channel based on visibility
                if (showRedChannel)
                {
                    var redScatter = wpfPlot.Plot.Add.Scatter(positions, rValues);
                    redScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Red);
                    redScatter.LineWidth = 2;
                    redScatter.Label = "R";
                }

                if (showGreenChannel)
                {
                    var greenScatter = wpfPlot.Plot.Add.Scatter(positions, gValues);
                    greenScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Green);
                    greenScatter.LineWidth = 2;
                    greenScatter.Label = "G";
                }

                if (showBlueChannel)
                {
                    var blueScatter = wpfPlot.Plot.Add.Scatter(positions, bValues);
                    blueScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Blue);
                    blueScatter.LineWidth = 2;
                    blueScatter.Label = "B";
                }

                wpfPlot.Plot.Title($"极角 {selectedPolarLine.Angle}° RGB分布曲线");
                wpfPlot.Plot.XLabel("角度 (°)");
                wpfPlot.Plot.YLabel("像素值");
                wpfPlot.Plot.Legend.IsVisible = true;
                wpfPlot.Plot.Axes.AutoScale();

                wpfPlot.Refresh();

                log.Info($"更新图表: 角度{selectedPolarLine.Angle}°");
            }
            catch (Exception ex)
            {
                log.Error($"更新图表失败: {ex.Message}", ex);
            }
        }
    }
}
