using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using log4net;
using Microsoft.Win32;
using OpenCvSharp.WpfExtensions;
using ProjectStarkSemi.Conoscope;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ProjectStarkSemi
{

    /// <summary>
    /// ConoscopeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ConoscopeWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeWindow));

        private MVSViewWindow? observationCameraWindow;
        private DeviceCamera? Device;

        // Polar angle line management
        private ObservableCollection<PolarAngleLine> polarAngleLines = new ObservableCollection<PolarAngleLine>();
        private PolarAngleLine? selectedPolarLine;
        
        // Concentric circle line management
        private ObservableCollection<ConcentricCircleLine> concentricCircleLines = new ObservableCollection<ConcentricCircleLine>();
        
        // Current image state for dynamic angle addition
        private BitmapSource? currentBitmapSource;
        private Point currentImageCenter;
        private int currentImageRadius;

        public double MaxAngle { get; set; } = 60;

        public ConoscopeWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        public ConoscopeManager ConoscopeManager => ConoscopeManager.GetInstance();
        public ConoscopeConfig ConoscopeConfig => ConoscopeManager.Config;

        private void Window_Initialized(object sender, EventArgs e)
        {
            ImageView.SetBackGround(Brushes.Transparent);
            if (ImageView.EditorContext.IEditorToolFactory.GetIEditorTool<ToolReferenceLine>() is ToolReferenceLine toolReferenceLine)
            {
                toolReferenceLine.ReferenceLine = new ReferenceLine(ConoscopeConfig.ReferenceLineParam);
            }
            ImageView.Config.IsToolBarAlVisible = false;
            ImageView.Config.IsToolBarLeftVisible = false;
            ImageView.Config.IsToolBarRightVisible = true;
            ImageView.Config.IsToolBarTopVisible = false;
            ImageView.Config.IsToolBarDrawVisible = false;

            cbModelType.ItemsSource = Enum.GetValues(typeof(ConoscopeModelType));
            this.DataContext = ConoscopeManager;

            LoadCameraServices();
            UpdateUIForModel(ConoscopeConfig.CurrentModel);

            wpfPlot.Plot.Title($"视角分布曲线");
            wpfPlot.Plot.XLabel("Degress");
            wpfPlot.Plot.YLabel("Luminance (cd/m²)");
            wpfPlot.Plot.Legend.FontName = ScottPlot.Fonts.Detect("中文");

            string fontSample = $"中文 Luminance Voltage";
            wpfPlot.Plot.Axes.Title.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            wpfPlot.Plot.Axes.Left.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            wpfPlot.Plot.Axes.Bottom.Label.FontName = ScottPlot.Fonts.Detect(fontSample);

            // Enable grid for better readability
            wpfPlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
            wpfPlot.Plot.Grid.MajorLineWidth = 1;
            wpfPlot.Plot.Axes.SetLimits(-80, 80, 0, 600);

            wpfPlot.Refresh();
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
            if (cbModelType.SelectedItem is ConoscopeModelType conoscopeModelType)
            {
                ConoscopeConfig.CurrentModel = conoscopeModelType;
                UpdateUIForModel(ConoscopeConfig.CurrentModel);
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
                    MaxAngle = 60;
                    wpfPlot.Plot.Axes.SetLimits(-MaxAngle, MaxAngle, 0, 600);
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
                    MaxAngle = 80;
                    wpfPlot.Plot.Axes.SetLimits(-MaxAngle, MaxAngle, 0, 600);
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

                // Convert WPF BitmapSource to OpenCV XYZMat
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

        public OpenCvSharp.Mat XMat { get; set; }
        public OpenCvSharp.Mat YMat { get; set; }
        public OpenCvSharp.Mat ZMat { get; set; }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filename = openFileDialog.FileName;
                if (CVFileUtil.IsCVCIEFile(filename))
                {
                    XMat?.Dispose();
                    YMat?.Dispose();
                    ZMat?.Dispose();

                    CVCIEFile fileInfo = new CVCIEFile();
                    CVFileUtil.Read(filename, out fileInfo);


                    
                    // Calculate the size of a single channel in bytes
                    int channelSize = fileInfo.Cols * fileInfo.Rows * (fileInfo.Bpp / 8);


                    OpenCvSharp.MatType singleChannelType;
                    switch (fileInfo.Bpp)
                    {
                        case 8: singleChannelType = OpenCvSharp.MatType.CV_8UC1; break;
                        case 16: singleChannelType = OpenCvSharp.MatType.CV_16UC1; break;
                        case 32: singleChannelType = OpenCvSharp.MatType.CV_32FC1; break; // Most likely for XYZ
                        case 64: singleChannelType = OpenCvSharp.MatType.CV_64FC1; break;
                        default: throw new NotSupportedException($"Bpp {fileInfo.Bpp} not supported");
                    }
                    if (fileInfo.Channels == 3)
                    {
                        byte[] dataX = new byte[channelSize];
                        byte[] dataY = new byte[channelSize];
                        byte[] dataZ = new byte[channelSize];

                        Buffer.BlockCopy(fileInfo.Data, 0, dataX, 0, channelSize);
                        Buffer.BlockCopy(fileInfo.Data, channelSize, dataY, 0, channelSize);
                        Buffer.BlockCopy(fileInfo.Data, channelSize * 2, dataZ, 0, channelSize);

                        XMat = OpenCvSharp.Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, singleChannelType, dataX);
                        YMat = OpenCvSharp.Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, singleChannelType, dataY);
                        ZMat = OpenCvSharp.Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, singleChannelType, dataZ);
                    }
                    else
                    {
                        byte[] dataX = new byte[channelSize];
                        Buffer.BlockCopy(fileInfo.Data, 0, dataX, 0, channelSize);
                        YMat = OpenCvSharp.Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, singleChannelType, dataX);
                    }

                }

                ImageView.OpenImage(filename);
                ImageView.ImageShow.ImageInitialized += (s, e) =>
                {
                    ImageView.Config.IsPseudo = true;
                    CreateAndAnalyzePolarLines();
                    Application.Current.Dispatcher.Invoke(async () =>
                    {
                        await Task.Delay(500);
                        if (ImageView.EditorContext.IEditorToolFactory.GetIEditorTool<ToolReferenceLine>() is ToolReferenceLine toolReferenceLine)
                        {
                            toolReferenceLine.IsChecked = true;
                        }
                    });
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
                int radius = (int)(MaxAngle / ConoscopeConfig.ConoscopeCoefficient);

                // Calculate center point
                Point center = new Point(imageWidth / 2.0, imageHeight / 2.0);

                // Store current image state for dynamic angle addition
                currentBitmapSource = bitmapSource;
                currentImageCenter = center;
                currentImageRadius = radius;

                log.Info($"图像尺寸: {imageWidth}x{imageHeight}, 中心: ({center.X}, {center.Y}), 半径: {radius}");

                foreach (var item in ConoscopeConfig.DefaultRAngles)
                {
                    CircleProperties circleProperties = new CircleProperties
                    {
                        Center = center,
                        Radius = radius * item / MaxAngle,
                        Pen = new Pen(Brushes.Yellow, 1 / ImageView.EditorContext.ZoomRatio),
                        Brush = Brushes.Transparent
                    };
                    DVCircle circle = new DVCircle(circleProperties);
                    ImageView.AddVisual(circle);
                }

                // Clear existing lines
                ClearPolarLines();

                // Create lines for each angle
                foreach (double angle in ConoscopeConfig.DefaultAngles)
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
            UpdatePlot();
        }

        /// <summary>
        /// 添加角度按钮点击事件
        /// </summary>
        private void btnAddAngle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if image is loaded
                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    log.Warn("未加载图像，无法添加角度线");
                    return;
                }

                // Parse angle from text box
                if (!double.TryParse(txtAngle.Text, out double angle))
                {
                    MessageBox.Show("请输入有效的角度数值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    log.Warn($"无效的角度输入: {txtAngle.Text}");
                    return;
                }

                // Normalize angle to 0-360 range
                angle = angle % 360;
                if (angle < 0) angle += 360;

                // Check if angle already exists
                if (polarAngleLines.Any(line => Math.Abs(line.Angle - angle) < 0.01))
                {
                    MessageBox.Show($"角度 {angle:F1}° 已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"角度 {angle:F1}° 已存在，跳过添加");
                    return;
                }



                // Create new line at specified angle
                CreatePolarLine(angle, currentImageCenter, currentImageRadius, currentBitmapSource);

                // Select the newly added line (ObservableCollection auto-updates the UI)
                var newLine = polarAngleLines.FirstOrDefault(line => Math.Abs(line.Angle - angle) < 0.01);
                if (newLine != null)
                {
                    cbPolarAngleLines.SelectedItem = newLine;
                }

                // Clear text box
                txtAngle.Text = "";
                ConoscopeConfig.DefaultAngles.Add(angle);
                log.Info($"成功添加角度线: {angle:F1}°");
            }
            catch (Exception ex)
            {
                log.Error($"添加角度失败: {ex.Message}", ex);
                MessageBox.Show($"添加角度失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除选中角度按钮点击事件
        /// </summary>
        private void btnRemoveAngle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedPolarLine == null)
                {
                    MessageBox.Show("请先选择要删除的角度", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Remove from visual
                if (selectedPolarLine.Line != null)
                {
                    ImageView.DrawingVisualLists.Remove(selectedPolarLine.Line);
                }

                double removedAngle = selectedPolarLine.Angle;

                // Remove from collection
                polarAngleLines.Remove(selectedPolarLine);

                // Select first line if available (ObservableCollection auto-updates the UI)
                if (polarAngleLines.Count > 0)
                {
                    selectedPolarLine = polarAngleLines[0];
                    cbPolarAngleLines.SelectedIndex = 0;
                    UpdatePlot();
                }
                else
                {
                    selectedPolarLine = null;
                    UpdatePlot();
                }

                log.Info($"成功删除角度线: {removedAngle:F1}°");
            }
            catch (Exception ex)
            {
                log.Error($"删除角度失败: {ex.Message}", ex);
                MessageBox.Show($"删除角度失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 按角度模式导出按钮点击事件
        /// </summary>
        private void btnExportAngleMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get selected export channel
                ExportChannel channel = GetSelectedExportChannel();

                // Open save file dialog
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"角度模式导出_{channel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportAngleModeToCSV(saveFileDialog.FileName, channel);
                    MessageBox.Show($"数据已成功导出到:\n{saveFileDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"成功导出角度模式CSV: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"角度模式导出失败: {ex.Message}", ex);
                MessageBox.Show($"角度模式导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 按同心圆模式导出按钮点击事件
        /// </summary>
        private void btnExportCircleMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get selected export channel
                ExportChannel channel = GetSelectedExportChannel();

                // Open save file dialog
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"同心圆模式导出_{channel}_{ConoscopeConfig.CurrentModel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportCircleModeToCSV(saveFileDialog.FileName, channel);
                    MessageBox.Show($"数据已成功导出到:\n{saveFileDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"成功导出同心圆模式CSV: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"同心圆模式导出失败: {ex.Message}", ex);
                MessageBox.Show($"同心圆模式导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取选中的导出通道
        /// </summary>
        private ExportChannel GetSelectedExportChannel()
        {
            if (cbExportChannel.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string channelTag)
            {
                if (Enum.TryParse<ExportChannel>(channelTag, out var channel))
                {
                    return channel;
                }
            }
            return ExportChannel.R;
        }

        /// <summary>
        /// 按角度模式导出数据到CSV文件
        /// 格式: Phi \ Theta 矩阵格式
        /// 行: Theta (采样点位置，从0到MaxAngle)
        /// 列: Phi (角度线，从0°到180°)
        /// </summary>
        private void ExportAngleModeToCSV(string filePath, ExportChannel channel)
        {
            if (currentBitmapSource == null)
            {
                log.Warn("没有图像数据，无法导出");
                return;
            }

            // Create angle lines from 0° to 180°
            var angleLines = CreateAngleLinesForExport();

            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                if (angleLines.Count == 0)
                {
                    log.Warn("没有角度线数据可导出");
                    return;
                }

                // Write header comments
                writer.WriteLine($"# 角度模式导出数据 (Phi \\ Theta 格式)");
                writer.WriteLine($"# 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"# 导出通道: {channel}");
                writer.WriteLine($"# 型号: {ConoscopeConfig.CurrentModel}");
                writer.WriteLine($"# 最大视角: {MaxAngle}°");
                writer.WriteLine($"# Phi (列): 角度线方向 (0°-180°)");
                writer.WriteLine($"# Theta (行): 采样点位置 (0 到 MaxAngle)");
                writer.WriteLine();

                // Write CSV header: Phi \ Theta, followed by each Phi angle (0-180)
                StringBuilder headerLine = new StringBuilder();
                headerLine.Append("Phi \\ Theta");
                foreach (var line in angleLines)
                {
                    headerLine.Append($",{line.Angle:F0}");
                }
                writer.WriteLine(headerLine.ToString());

                // Find the maximum number of samples across all lines
                int maxSamples = angleLines.Max(l => l.RgbData.Count);
                if (maxSamples == 0) return;

                // Export each row (Theta position from 0 to MaxAngle)
                for (int i = 0; i < maxSamples; i++)
                {
                    StringBuilder dataLine = new StringBuilder();
                    
                    // Get Theta position from first line
                    double theta = angleLines[0].RgbData.Count > i ? angleLines[0].RgbData[i].Position : 0;
                    dataLine.Append($"{theta:F2}");

                    // Add value for each Phi angle
                    foreach (var line in angleLines)
                    {
                        if (line.RgbData.Count > i)
                        {
                            double value = GetChannelValue(line.RgbData[i], channel);
                            dataLine.Append($",{value:F2}");
                        }
                        else
                        {
                            dataLine.Append(",");
                        }
                    }
                    writer.WriteLine(dataLine.ToString());
                }

                log.Info($"角度模式导出了 {angleLines.Count} 个Phi角度 (0°-180°) 的数据, 通道: {channel}");
            }
        }

        /// <summary>
        /// 为导出创建从0°到180°的角度线数据
        /// 每条线采样从中心点(0)到边缘(MaxAngle)
        /// </summary>
        private List<PolarAngleLine> CreateAngleLinesForExport()
        {
            var angleLines = new List<PolarAngleLine>();

            if (currentBitmapSource == null) return angleLines;

            OpenCvSharp.Mat mat = BitmapSourceConverter.ToMat(currentBitmapSource);

            try
            {
                int numSamples = (int)MaxAngle + 1; // 0 to MaxAngle inclusive
                int radius = (int)(MaxAngle / ConoscopeConfig.ConoscopeCoefficient);

                // Create lines from 0° to 180° (181 lines)
                for (int phi = 0; phi <= 180; phi++)
                {
                    PolarAngleLine polarLine = new PolarAngleLine
                    {
                        Angle = phi
                    };

                    double radians = phi * Math.PI / 180.0;

                    // Sample from center (theta=0) to edge (theta=MaxAngle)
                    for (int theta = 0; theta <= (int)MaxAngle; theta++)
                    {
                        double radiusPixels = theta / ConoscopeConfig.ConoscopeCoefficient;
                        double x = currentImageCenter.X + radiusPixels * Math.Cos(radians);
                        double y = currentImageCenter.Y + radiusPixels * Math.Sin(radians);

                        int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                        int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                        double r = 0, g = 0, b = 0;
                        double X = 0, Y = 0, Z = 0;

                        ExtractPixelValues(mat, ix, iy, out r, out g, out b, out X, out Y, out Z);

                        polarLine.RgbData.Add(new RgbSample
                        {
                            Position = theta, // 0 to MaxAngle
                            R = r,
                            G = g,
                            B = b,
                            X = X,
                            Y = Y,
                            Z = Z
                        });
                    }

                    angleLines.Add(polarLine);
                }

                log.Info($"创建了 {angleLines.Count} 条角度线 (0°-180°) 用于导出");
            }
            finally
            {
                mat.Dispose();
            }

            return angleLines;
        }

        /// <summary>
        /// 按同心圆模式导出数据到CSV文件
        /// 格式: Phi \ Theta 矩阵格式
        /// 行: Theta (圆周角度，0-359°)
        /// 列: Phi (半径角度，0-60或0-80，包含0度中心点)
        /// </summary>
        private void ExportCircleModeToCSV(string filePath, ExportChannel channel)
        {
            // Create concentric circles and extract data
            CreateConcentricCirclesData();

            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                if (concentricCircleLines.Count == 0)
                {
                    log.Warn("没有同心圆数据可导出");
                    return;
                }

                var sortedCircles = concentricCircleLines.OrderBy(c => c.RadiusAngle).ToList();

                // Write header comments
                writer.WriteLine($"# 同心圆模式导出数据 (Phi \\ Theta 格式)");
                writer.WriteLine($"# 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"# 导出通道: {channel}");
                writer.WriteLine($"# 型号: {ConoscopeConfig.CurrentModel}");
                writer.WriteLine($"# 最大视角: {MaxAngle}°");
                writer.WriteLine($"# 同心圆数量: {sortedCircles.Count} (包含0度中心点)");
                writer.WriteLine($"# Phi (列): 半径角度 (视角, 0-{MaxAngle}°)");
                writer.WriteLine($"# Theta (行): 圆周角度 (0-359°)");
                writer.WriteLine();

                // Write CSV header: Phi \ Theta, followed by each Phi (radius angle)
                StringBuilder headerLine = new StringBuilder();
                headerLine.Append("Phi \\ Theta");
                foreach (var circle in sortedCircles)
                {
                    headerLine.Append($",{circle.RadiusAngle:F0}");
                }
                writer.WriteLine(headerLine.ToString());

                // Export each row (Theta = 0-359)
                for (int theta = 0; theta <= 360; theta++)
                {
                    StringBuilder dataLine = new StringBuilder();
                    dataLine.Append($"{theta}");

                    // Add value for each Phi (radius angle)
                    foreach (var circle in sortedCircles)
                    {
                        if (circle.RgbData.Count > theta)
                        {
                            double value = GetChannelValue(circle.RgbData[theta], channel);
                            dataLine.Append($",{value:F2}");
                        }
                        else
                        {
                            dataLine.Append(",");
                        }
                    }
                    writer.WriteLine(dataLine.ToString());
                }

                log.Info($"同心圆模式导出了 {sortedCircles.Count} 个Phi角度 x 360 Theta的数据, 通道: {channel}");
            }
        }

        /// <summary>
        /// 获取指定通道的值
        /// </summary>
        private double GetChannelValue(RgbSample sample, ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.R => sample.R,
                ExportChannel.G => sample.G,
                ExportChannel.B => sample.B,
                ExportChannel.X => sample.X,
                ExportChannel.Y => sample.Y,
                ExportChannel.Z => sample.Z,
                _ => sample.R // Default to R channel
            };
        }

        /// <summary>
        /// 创建同心圆数据
        /// VA60: 61个同心圆 (每度一个，从0度到60度)
        /// VA80: 81个同心圆 (每度一个，从0度到80度)
        /// 0度为中心点，使用插值
        /// </summary>
        private void CreateConcentricCirclesData()
        {
            concentricCircleLines.Clear();

            if (currentBitmapSource == null) return;

            // Convert BitmapSource to OpenCV XYZMat
            OpenCvSharp.Mat mat = BitmapSourceConverter.ToMat(currentBitmapSource);

            try
            {
                // Calculate the number of concentric circles based on model
                int numCircles = (int)MaxAngle;

                // For each degree from 0 to MaxAngle, create a concentric circle
                // 0 degree is center point
                for (int degree = 0; degree <= numCircles; degree++)
                {
                    ConcentricCircleLine circleLine = new ConcentricCircleLine
                    {
                        RadiusAngle = degree
                    };

                    if (degree == 0)
                    {
                        // Center point (0 degree): Use the center pixel value for all 360 samples
                        int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(currentImageCenter.X)));
                        int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(currentImageCenter.Y)));

                        double r = 0, g = 0, b = 0;
                        double X = 0, Y = 0, Z = 0;

                        ExtractPixelValues(mat, ix, iy, out r, out g, out b, out X, out Y, out Z);

                        // Fill all 360 samples with the center point value
                        for (int anglePos = 0; anglePos < 360; anglePos++)
                        {
                            circleLine.RgbData.Add(new RgbSample
                            {
                                Position = anglePos, // 0 to MaxAngle
                                R = r,
                                G = g,
                                B = b,
                                X = X,
                                Y = Y,
                                Z = Z
                            });
                        }
                    }
                    else
                    {
                        // Calculate radius in pixels for this degree angle
                        double radiusPixels = degree / ConoscopeConfig.ConoscopeCoefficient;

                        // Sample points along the circle (360 samples for full circle, one per degree)
                        for (int anglePos = 0; anglePos < 360; anglePos++)
                        {
                            double radians = anglePos * Math.PI / 180.0;
                            double x = currentImageCenter.X + radiusPixels * Math.Cos(radians);
                            double y = currentImageCenter.Y + radiusPixels * Math.Sin(radians);

                            // Ensure coordinates are within bounds
                            int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                            int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));


                            double r = 0, g = 0, b = 0;
                            double X = 0, Y = 0, Z = 0;

                            ExtractPixelValues(mat, ix, iy, out r, out g, out b, out X, out Y, out Z);
                            circleLine.RgbData.Add(new RgbSample{ Position = anglePos,  R = r, G = g,   B = b, X = X, Y = Y, Z = Z });

       
                        }
                    }

                    concentricCircleLines.Add(circleLine);
                }

                log.Info($"创建了 {concentricCircleLines.Count} 个同心圆数据 (包含0度中心点)");
            }
            finally
            {
                mat.Dispose();
            }
        }

        /// <summary>
        /// 从Mat中提取像素值
        /// </summary>
        private void ExtractPixelValues(OpenCvSharp.Mat mat, int ix, int iy, out double r, out double g, out double b,out double X,out double Y,out double Z)
        {
            r = 0; g = 0; b = 0;
            X = Y = Z = 0;
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
                if (YMat != null)
                    Y = YMat.At<float>(iy, ix);
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

                if (XMat != null)
                    X = XMat.At<float>(iy, ix);
                if (YMat != null)
                    Y = YMat.At<float>(iy, ix);
                if (ZMat != null)
                    Z = ZMat.At<float>(iy, ix);
            }
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
            line.Pen = new Pen(Brushes.Yellow,0.5/ ImageView.EditorContext.ZoomRatio);
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
                // Convert BitmapSource to OpenCV RGBMat
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
                    double X = 0, Y = 0, Z = 0;

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

                        if (XMat != null)
                            X = XMat.At<float>(iy, ix);
                        if (YMat != null)
                            Y = YMat.At<float>(iy, ix);
                        if (ZMat != null)
                            Z = ZMat.At<float>(iy, ix);
                    }

                    polarLine.RgbData.Add(new RgbSample
                    {
                        Position = position,
                        R = r,
                        G = g,
                        B = b,
                        X = X,
                        Y = Y,
                        Z =Z,
                    });
                }

                mat.Dispose();
                log.Info($"完成RGB采样: 角度{polarLine.Angle}°, 采样点数{polarLine.RgbData.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取数据失败: {ex.Message}", ex);
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

                // Add scatter plots for each channel based on visibility
                if (ConoscopeConfig.IsShowRedChannel)
                {
                    double[] rValues = selectedPolarLine.RgbData.Select(s => s.R).ToArray();
                    var redScatter = wpfPlot.Plot.Add.Scatter(positions, rValues);
                    redScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Red);
                    redScatter.LineWidth = 2;
                    redScatter.LegendText = "R";
                }

                if (ConoscopeConfig.IsShowGreenChannel)
                {
                    double[] gValues = selectedPolarLine.RgbData.Select(s => s.G).ToArray();

                    var greenScatter = wpfPlot.Plot.Add.Scatter(positions, gValues);
                    greenScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Green);
                    greenScatter.LineWidth = 2;
                    greenScatter.LegendText = "G";
                }

                if (ConoscopeConfig.IsShowBlueChannel)
                {
                    double[] bValues = selectedPolarLine.RgbData.Select(s => s.B).ToArray();
                    var blueScatter = wpfPlot.Plot.Add.Scatter(positions, bValues);
                    blueScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Blue);
                    blueScatter.LineWidth = 2;
                    blueScatter.LegendText = "B";
                }
                if (ConoscopeConfig.IsShowXChannel)
                {
                    double[] XValues = selectedPolarLine.RgbData.Select(s => s.X).ToArray();
                    var blueScatter = wpfPlot.Plot.Add.Scatter(positions, XValues);
                    blueScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gold);
                    blueScatter.LineWidth = 2;
                    blueScatter.LegendText = "X";
                }
                if (ConoscopeConfig.IsShowYChannel)
                {
                    double[] YValues = selectedPolarLine.RgbData.Select(s => s.Y).ToArray();
                    var blueScatter = wpfPlot.Plot.Add.Scatter(positions, YValues);
                    blueScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
                    blueScatter.LineWidth = 2;
                    blueScatter.LegendText = "Y";
                }
                if (ConoscopeConfig.IsShowZChannel)
                {
                    double[] ZValues = selectedPolarLine.RgbData.Select(s => s.Z).ToArray();
                    var blueScatter = wpfPlot.Plot.Add.Scatter(positions, ZValues);
                    blueScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Violet);
                    blueScatter.LineWidth = 2;
                    blueScatter.LegendText = "Z";
                }

                wpfPlot.Plot.Title($"极角 {selectedPolarLine.Angle}°分布曲线");
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

        private void RibbonButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("视角测量");
        }
        public void Dispose()
        {
            XMat?.Dispose();
            XMat = null;
            YMat?.Dispose();
            YMat = null;
            ZMat?.Dispose();
            ZMat = null;
            ImageView?.Dispose();
            GC.SuppressFinalize(this);
        }


        private void Window_Closed(object sender, EventArgs e)
        {
            this.Dispose();
        }
    }
}
