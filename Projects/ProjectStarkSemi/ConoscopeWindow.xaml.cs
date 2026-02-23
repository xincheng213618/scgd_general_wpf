using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using log4net;
using Microsoft.Win32;
using OpenCvSharp.WpfExtensions;
using ProjectStarkSemi.Conoscope;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectStarkSemi
{
    public class ConoscopeWindowConfig : WindowConfig
    {
        public static ConoscopeWindowConfig Instance => ConfigService.Instance.GetRequiredService<ConoscopeWindowConfig>();
    }

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
        private ConcentricCircleLine? selectedCircleLine;
        
        // Displayed circles for UI management
        private ObservableCollection<ConcentricCircleLine> displayedCircles = new ObservableCollection<ConcentricCircleLine>();
        
        // Current image state for dynamic angle addition
        private BitmapSource? currentBitmapSource;
        private Point currentImageCenter;
        private int currentImageRadius;

        public double MaxAngle
        {
            get
            {
                if (ConoscopeConfig.CurrentModel == ConoscopeModelType.VA80)
                {
                    return 80;
                }
                else if (ConoscopeConfig.CurrentModel == ConoscopeModelType.VA60)
                {
                    return 60;
                }
                return 80;
            }
        }

        public ConoscopeWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            ConoscopeWindowConfig.Instance.SetWindow(this);
            this.Title += Assembly.GetAssembly(typeof(ConoscopeWindow))?.GetName().Version?.ToString() ?? "";
        }
        public ConoscopeConfig ConoscopeConfig => ConoscopeManager.GetInstance().Config;

        private void Window_Initialized(object sender, EventArgs e)
        {
            foreach (var item in ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>())
            {
                StackPanelControl.Children.Add(item.GetDisplayCamera());

                this.Closed += (s, e) =>
                {
                    item.MsgRecordChanged -= Item_MsgRecordChanged;
                };
                item.MsgRecordChanged -= Item_MsgRecordChanged;
                item.MsgRecordChanged += Item_MsgRecordChanged;
            }

            foreach (var item in ServiceManager.GetInstance().DeviceServices.OfType<DeviceCfwPort>())
            {
                StackPanelControl.Children.Add(item.GetDisplayControl());
            }
            FlowEngineManager.GetInstance().BatchRecord -= ConoscopeWindow_BatchRecord;
            FlowEngineManager.GetInstance().BatchRecord += ConoscopeWindow_BatchRecord;


            ImageView.SetBackGround(Brushes.Transparent);
            try
            {
                if (ImageView.EditorContext.IEditorToolFactory.GetIEditorTool<ToolReferenceLine>() is ToolReferenceLine toolReferenceLine)
                {
                    toolReferenceLine.ReferenceLine = new ReferenceLine(ConoscopeConfig.ReferenceLineParam);
                }

            }
            catch(Exception ex)
            {
                log.Info(ex);
            }

            GridSetting.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(ConoscopeConfig.ReferenceLineParam));

            if (ImageView.EditorContext.IEditorToolFactory.GetIEditorTool<MouseMagnifierManager>() is MouseMagnifierManager  mouseMagnifierManager)
            {
                mouseMagnifierManager.IsChecked = true;
            }

            ImageView.Config.IsToolBarAlVisible = false;
            ImageView.Config.IsToolBarLeftVisible = false;
            ImageView.Config.IsToolBarRightVisible = true;
            ImageView.Config.IsToolBarTopVisible = false;
            ImageView.Config.IsToolBarDrawVisible = false;

            cbModelType.ItemsSource = Enum.GetValues(typeof(ConoscopeModelType));
            this.DataContext = ConoscopeManager.GetInstance();

            LoadCameraServices();

            ConoscopeConfig.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig.ModelTypeChanged += ConoscopeConfig_ModelTypeChanged;
            ConoscopeConfig_ModelTypeChanged(sender, ConoscopeConfig.CurrentModel);

            this.Closed += (s, e) =>
            {
                ConoscopeConfig.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            };
            InitializePlot(wpfPlotDiameterLine, "方位角分布曲线 (Azimuth Distribution)"); ;
            InitializePlot(wpfPlotRCircle, "极角分布曲线 (Polar Angle Distribution)");
        }

        private void ConoscopeConfig_ModelTypeChanged(object? sender, ConoscopeModelType e)
        {
            if (tbCurrentModel == null) return;
            tbCurrentModel.Text = ConoscopeConfig.CurrentModel.ToString();
            switch (ConoscopeConfig.CurrentModel)
            {
                case ConoscopeModelType.VA60:
                    wpfPlotDiameterLine.Plot.Axes.SetLimits(-MaxAngle, MaxAngle, 0, 600);
                    wpfPlotRCircle.Plot.Axes.SetLimits(0, 360, 0, 600);
                    break;

                case ConoscopeModelType.VA80:
                    wpfPlotDiameterLine.Plot.Axes.SetLimits(-MaxAngle, MaxAngle, 0, 600);
                    wpfPlotRCircle.Plot.Axes.SetLimits(0, 360, 0, 600);
                    break;
            }
        }

        private void Item_MsgRecordChanged(object? sender, MsgRecord e)
        {
            e.MsgSucessed += (s,e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    int masterId = Convert.ToInt32(e.Data.MasterId);
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
                        string filename = string.Empty;
                        foreach (MeasureResultImgModel result in resultMaster)
                        {
                            if (CVFileUtil.IsCVCIEFile(result.FileUrl))
                            {
                                filename = result.FileUrl;
                                break;
                            }
                        }
                        if (string.IsNullOrEmpty(filename))
                        {
                            filename = resultMaster[0].FileUrl;
                        }
                        OpenConoscope(filename);
                    }
                    else
                    {
                        log.Warn("未获取到图像数据");
                    }
                });
            };
        }

        private void ConoscopeWindow_BatchRecord(object? sender, MeasureBatchModel e)
        {
            e.FlowStatusChaned += (s, e1) =>
            {
                if (e.FlowStatus == FlowStatus.Completed)
                {
                    var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                    var MeasureResultImgModels = DB.Queryable<MeasureResultImgModel>()
                        .Where(x => x.BatchId == e.Id)
                        .ToList();
                    DB.Dispose();

                    var model = MeasureResultImgModels.Last();
                    if (model != null)
                    {
                        OpenConoscope(model.FileUrl);
                    }
                }
            };
        }

        private void InitializePlot(ScottPlot.WPF.WpfPlot plot, string title)
        {
            plot.Plot.Title(title);
            plot.Plot.XLabel("Degrees");
            plot.Plot.YLabel("Luminance (cd/m²)");
            plot.Plot.Legend.FontName = ScottPlot.Fonts.Detect("中文");

            string fontSample = $"中文 Luminance Voltage";
            plot.Plot.Axes.Title.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            plot.Plot.Axes.Left.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            plot.Plot.Axes.Bottom.Label.FontName = ScottPlot.Fonts.Detect(fontSample);

            // Enable grid for better readability
            plot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
            plot.Plot.Grid.MajorLineWidth = 1;
            plot.Plot.Axes.SetLimits(-MaxAngle, MaxAngle, 0, 600);

            plot.Refresh();
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
                ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams.CreateEmpty();
                ComboxCalibrationTemplate.SelectedIndex = 0;
            }
        }


        private void cbModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbModelType.SelectedItem is ConoscopeModelType conoscopeModelType)
            {
                ConoscopeConfig.CurrentModel = conoscopeModelType;
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
              
                double[] expTime = new double[] { Device.DisplayConfig.ExpTime };
                AutoExpTimeParam autoExpTimeParam = new AutoExpTimeParam { Id = -1 };
                ParamBase hdrParam = new ParamBase { Id = -1 };

                MsgRecord msgRecord = Device.DService.GetData(expTime, param, autoExpTimeParam, hdrParam);

                if (msgRecord != null)
                {
                    tbMeasurementCameraStatus.Text = "正在获取...";
                    tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Orange);
                    msgRecord.MsgSucessed += (s,e) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            int masterId = Convert.ToInt32(e.Data.MasterId);
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
                                string filename = string.Empty;
                                foreach (MeasureResultImgModel result in resultMaster)
                                {
                                    if (CVFileUtil.IsCVCIEFile(result.FileUrl))
                                    {
                                        filename = result.FileUrl;
                                        break;
                                    }
                                }
                                if (string.IsNullOrEmpty(filename))
                                {
                                    filename = resultMaster[0].FileUrl;
                                }
                                OpenConoscope(filename);
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
                OpenConoscope(filename);
            }       
        }
        string Filename;
        private void OpenConoscope(string filename)
        {
            Filename = filename;
            ImageView.Clear();
            ImageView.ImageShow.ImageInitialized -= ImageShow_ImageInitialized;
            ImageView.ImageShow.ImageInitialized += ImageShow_ImageInitialized;
            ImageView.OpenImage(filename);
        }

        private void ImageShow_ImageInitialized(object? sender, EventArgs e)
        {
            ImageView.Config.IsPseudo = true;

            if (CVFileUtil.IsCVCIEFile(Filename))
            {
                XMat?.Dispose();
                YMat?.Dispose();
                ZMat?.Dispose();

                CVCIEFile fileInfo = new CVCIEFile();
                CVFileUtil.Read(Filename, out fileInfo);

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

                    XMat = OpenCvSharp.Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, singleChannelType, dataX);

                    YMat = OpenCvSharp.Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, singleChannelType, dataY);

                    ZMat = OpenCvSharp.Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, singleChannelType, dataZ);
                }
                else
                {
                    byte[] dataX = new byte[channelSize];
                    Buffer.BlockCopy(fileInfo.Data, 0, dataX, 0, channelSize);
                    YMat = OpenCvSharp.Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, singleChannelType, dataX);
                }

            }

            CreateAndAnalyzePolarLines();
            Application.Current.Dispatcher.Invoke(async () =>
            {
                await Task.Delay(200);
                if (ImageView.EditorContext.IEditorToolFactory.GetIEditorTool<ToolReferenceLine>() is ToolReferenceLine toolReferenceLine)
                {
                    toolReferenceLine.IsChecked = false;
                    toolReferenceLine.IsChecked = true;
                }
            });
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

                // Clear existing displayed circles
                ClearDisplayedCircles();

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
                    
                    // Add to displayed circles collection for management
                    ConcentricCircleLine circleLine = new ConcentricCircleLine
                    {
                        RadiusAngle = item,
                        Circle = circle
                    };
                    
                    // Extract RGB data along the circle
                    ExtractRgbAlongCircle(circleLine, center, item, bitmapSource);
                    
                    displayedCircles.Add(circleLine);
                }
                
                // Set up circles ComboBox
                if (displayedCircles.Count > 0)
                {
                    cbConcentricCircles.ItemsSource = displayedCircles;
                    cbConcentricCircles.SelectedIndex = 0;
                    selectedCircleLine = displayedCircles[0];
                    // Update the R circle plot with the first circle's data
                    UpdatePlotForCircle();
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
                // Reset all lines to yellow first
                foreach (var line in polarAngleLines)
                {
                    if (line.Line != null)
                    {
                        line.Line.Pen = new Pen(Brushes.Yellow, 0.5 / ImageView.EditorContext.ZoomRatio);
                        line.Line.Render();
                    }
                }
                
                // Set selected line to red
                if (selectedLine.Line != null)
                {
                    selectedLine.Line.Pen = new Pen(Brushes.Red, 0.5 / ImageView.EditorContext.ZoomRatio);
                    selectedLine.Line.Render();
                }
                
                selectedPolarLine = selectedLine;
                UpdatePlot();
            }
        }

        /// <summary>
        /// RGB通道可见性改变事件
        /// </summary>
        private void RgbChannelVisibility_Changed(object sender, RoutedEventArgs e)
        {
            // If multiple selection is not allowed, implement exclusive selection behavior
            if (!ConoscopeConfig.AllowMultipleChannelSelection && sender is CheckBox changedCheckBox)
            {
                // If a checkbox was checked (not unchecked), uncheck all others
                if (changedCheckBox.IsChecked == true)
                {
                    // Uncheck all other checkboxes except the one that was just checked
                    if (changedCheckBox != chkShowRed)
                        chkShowRed.IsChecked = false;
                    if (changedCheckBox != chkShowGreen)
                        chkShowGreen.IsChecked = false;
                    if (changedCheckBox != chkShowBlue)
                        chkShowBlue.IsChecked = false;
                    if (changedCheckBox != chkShowXlue)
                        chkShowXlue.IsChecked = false;
                    if (changedCheckBox != chkShowYlue)
                        chkShowYlue.IsChecked = false;
                    if (changedCheckBox != chkShowZlue)
                        chkShowZlue.IsChecked = false;
                }
            }
            
            UpdatePlot();
            UpdatePlotForCircle();
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
        /// 添加极角角度按钮点击事件
        /// </summary>
        private void btnAddCircleAngle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if image is loaded
                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    log.Warn("未加载图像，无法添加极角");
                    return;
                }

                // Parse radius angle from text box
                if (!double.TryParse(txtCircleAngle.Text, out double radiusAngle))
                {
                    MessageBox.Show("请输入有效的半径角度数值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    log.Warn($"无效的半径角度输入: {txtCircleAngle.Text}");
                    return;
                }

                // Validate radius angle is within valid range (0 to MaxAngle)
                if (radiusAngle < 0 || radiusAngle > MaxAngle)
                {
                    MessageBox.Show($"半径角度必须在 0 到 {MaxAngle} 度之间", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    log.Warn($"半径角度超出范围: {radiusAngle}");
                    return;
                }

                // Check if radius angle already exists
                if (displayedCircles.Any(circle => Math.Abs(circle.RadiusAngle - radiusAngle) < 0.01))
                {
                    MessageBox.Show($"半径角度 {radiusAngle:F1}° 已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"半径角度 {radiusAngle:F1}° 已存在，跳过添加");
                    return;
                }

                // Create new circle at specified radius angle
                CircleProperties circleProperties = new CircleProperties
                {
                    Center = currentImageCenter,
                    Radius = currentImageRadius * radiusAngle / MaxAngle,
                    Pen = new Pen(Brushes.Yellow, 1 / ImageView.EditorContext.ZoomRatio),
                    Brush = Brushes.Transparent
                };
                DVCircle circle = new DVCircle(circleProperties);
                ImageView.AddVisual(circle);

                // Add to displayed circles collection
                ConcentricCircleLine newCircle = new ConcentricCircleLine
                {
                    RadiusAngle = radiusAngle,
                    Circle = circle
                };
                
                // Extract RGB data along the circle
                ExtractRgbAlongCircle(newCircle, currentImageCenter, radiusAngle, currentBitmapSource);
                
                // Insert the circle in sorted order by radius angle
                int insertIndex = 0;
                for (int i = 0; i < displayedCircles.Count; i++)
                {
                    if (displayedCircles[i].RadiusAngle > radiusAngle)
                    {
                        insertIndex = i;
                        break;
                    }
                    insertIndex = i + 1;
                }
                displayedCircles.Insert(insertIndex, newCircle);

                // Select the newly added circle
                cbConcentricCircles.SelectedItem = newCircle;
                selectedCircleLine = newCircle;
                
                // Update the R circle plot with the new circle's data
                UpdatePlotForCircle();

                // Add to config for persistence
                if (!ConoscopeConfig.DefaultRAngles.Contains(radiusAngle))
                {
                    ConoscopeConfig.DefaultRAngles.Add(radiusAngle);
                }

                // Clear text box
                txtCircleAngle.Text = "";
                
                log.Info($"成功添加极角: 半径角度 {radiusAngle:F1}°");
            }
            catch (Exception ex)
            {
                log.Error($"添加极角失败: {ex.Message}", ex);
                MessageBox.Show($"添加极角失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除选中极角按钮点击事件
        /// </summary>
        private void btnRemoveCircleAngle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedCircleLine == null)
                {
                    MessageBox.Show("请先选择要删除的极角", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Remove from visual
                if (selectedCircleLine.Circle != null)
                {
                    ImageView.DrawingVisualLists.Remove(selectedCircleLine.Circle);
                }

                double removedAngle = selectedCircleLine.RadiusAngle;

                // Remove from collection
                displayedCircles.Remove(selectedCircleLine);

                // Remove from config
                ConoscopeConfig.DefaultRAngles.Remove(removedAngle);

                // Select first circle if available
                if (displayedCircles.Count > 0)
                {
                    selectedCircleLine = displayedCircles[0];
                    cbConcentricCircles.SelectedIndex = 0;
                }
                else
                {
                    selectedCircleLine = null;
                }

                log.Info($"成功删除极角: 半径角度 {removedAngle:F1}°");
            }
            catch (Exception ex)
            {
                log.Error($"删除极角失败: {ex.Message}", ex);
                MessageBox.Show($"删除极角失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 极角选择改变事件
        /// </summary>
        private void cbConcentricCircles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbConcentricCircles.SelectedItem is ConcentricCircleLine selectedCircle)
            {
                // Reset all circles to yellow first
                foreach (var circle in displayedCircles)
                {
                    if (circle.Circle != null)
                    {
                        circle.Circle.Attribute.Pen = new Pen(Brushes.Yellow, 1 / ImageView.EditorContext.ZoomRatio);
                        circle.Circle.Render();
                    }
                }
                
                // Set selected circle to red
                if (selectedCircle.Circle != null)
                {
                    selectedCircle.Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / ImageView.EditorContext.ZoomRatio);
                    selectedCircle.Circle.Render();
                }
                
                selectedCircleLine = selectedCircle;
                log.Info($"选中极角: 半径角度 {selectedCircle.RadiusAngle:F1}°");
                UpdatePlotForCircle();
            }
        }

        /// <summary>
        /// 清除所有显示的极角
        /// </summary>
        private void ClearDisplayedCircles()
        {
            foreach (var circleLine in displayedCircles)
            {
                if (circleLine.Circle != null)
                {
                    ImageView.DrawingVisualLists.Remove(circleLine.Circle);
                }
            }
            displayedCircles.Clear();
            selectedCircleLine = null;
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
                    FileName = $"DiameterLine_Export_{channel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportAngleModeToCSV(saveFileDialog.FileName, channel);
                    MessageBox.Show($"数据已成功导出到:\n{saveFileDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"成功导出方位角模式CSV: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"方位角模式导出失败: {ex.Message}", ex);
                MessageBox.Show($"方位角模式导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 按极角模式导出按钮点击事件
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
                    FileName = $"RCircle_Export_{channel}_{ConoscopeConfig.CurrentModel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportCircleModeToCSV(saveFileDialog.FileName, channel);
                    MessageBox.Show($"数据已成功导出到:\n{saveFileDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"成功导出极角模式CSV: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"极角模式导出失败: {ex.Message}", ex);
                MessageBox.Show($"极角模式导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                writer.WriteLine($"# Azimuth Export Data (Phi \\ Theta Format)");
                writer.WriteLine($"# Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"# Export Channel: {channel}");
                writer.WriteLine($"# Model: {ConoscopeConfig.CurrentModel}");
                writer.WriteLine($"# Max Angle: {MaxAngle}°");
                writer.WriteLine($"# Phi (Column): Diameter line direction (0°-180°)");
                writer.WriteLine($"# Theta (Row): Sample point position (0 to MaxAngle)");
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

                log.Info($"方位角模式导出了 {angleLines.Count} 个Phi角度 (0°-180°) 的数据, 通道: {channel}");
            }
        }

        /// <summary>
        /// 为导出创建从0°到180°的方位角数据
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

                    // Add 90° to place 0° at the top (first quadrant/north)
                    // Negate to make rotation counter-clockwise in screen coordinates
                    double radians = (180 - phi) * Math.PI / 180.0;

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

                log.Info($"创建了 {angleLines.Count} 条方位角 (0°-180°) 用于导出");
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
                writer.WriteLine($"# Polar Angle Export Data (Phi \\ Theta Format)");
                writer.WriteLine($"# Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"# Export Channel: {channel}");
                writer.WriteLine($"# Model: {ConoscopeConfig.CurrentModel}");
                writer.WriteLine($"# Max Angle: {MaxAngle}°");
                writer.WriteLine($"# Polar Angle Count: {sortedCircles.Count} (including 0-degree center point)");
                writer.WriteLine($"# Phi (Column): Radius angle (viewing angle, 0-{MaxAngle}°)");
                writer.WriteLine($"# Theta (Row): Circumferential angle (0-359°)");
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

                log.Info($"极角模式导出了 {sortedCircles.Count} 个Phi角度 x 360 Theta的数据, 通道: {channel}");
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
                        for (int anglePos = 0; anglePos <= 360; anglePos++)
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
                        for (int anglePos = 0; anglePos <= 360; anglePos++)
                        {
                            // Add 90° to place 0° at the top (first quadrant/north)
                            // Negate to make rotation counter-clockwise in screen coordinates
                            double radians = (90 - anglePos) * Math.PI / 180.0;
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

                log.Info($"创建了 {concentricCircleLines.Count} 个极角数据 (包含0度中心点)");
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
            // Add 90° to place 0° at the top (first quadrant/north)
            // Negate to make rotation counter-clockwise in screen coordinates
            double radians = (180 - angle) * Math.PI / 180.0;

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

                PixelFormat PixelFormat = ImageView.EditorContext.Config.GetProperties<PixelFormat>("PixelFormat");

                // Sample points along the line
                for (int i = 0; i < numSamples; i++)
                {
                    double t = i / (double)(numSamples - 1);
                    double x = start.X + t * (end.X - start.X);
                    double y = start.Y + t * (end.Y - start.Y);

                    // Ensure coordinates are within bounds
                    int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                    int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                    if (ix == 0)
                    {
                        log.Info(ix);
                    }

                    double position = - MaxAngle + (i / (double)(numSamples - 1)) * MaxAngle * 2;

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
                            switch (PixelFormat.ToString())
                            {
                                case "Bgr32":
                                case "Bgra32":
                                case "Pbgra32":
                                case "Bgr24":
                                    b = pixel.Item0;
                                    g = pixel.Item1;
                                    r = pixel.Item2;
                                    break;
                                case "Rgb24":
                                case "Rgb48":
                                    r = pixel.Item0;
                                    g = pixel.Item1;
                                    b = pixel.Item2;
                                    break;
                                case "Gray8":
                                    break;
                                case "Gray16":
                                    break;
                                case "Gray32Float":
                                    break;
                                default:
                                    b = pixel.Item0;
                                    g = pixel.Item1;
                                    r = pixel.Item2;
                                    break;
                            }
  
                        }
                        else if (mat.Depth() == OpenCvSharp.MatType.CV_16U)
                        {
                            OpenCvSharp.Vec3w pixel = mat.At<OpenCvSharp.Vec3w>(iy, ix);
                            switch (PixelFormat.ToString())
                            {
                                case "Bgr32":
                                case "Bgra32":
                                case "Pbgra32":
                                case "Bgr24":
                                    b = pixel.Item0;
                                    g = pixel.Item1;
                                    r = pixel.Item2;
                                    break;
                                case "Rgb24":
                                case "Rgb48":
                                    r = pixel.Item0;
                                    g = pixel.Item1;
                                    b = pixel.Item2;
                                    break;
                                case "Gray8":
                                    break;
                                case "Gray16":
                                    break;
                                case "Gray32Float":
                                    break;
                                default:
                                    b = pixel.Item0;
                                    g = pixel.Item1;
                                    r = pixel.Item2;
                                    break;
                            }
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
                        DX =ix,
                        DY =iy,
                        R = r,
                        G = g,
                        B = b,
                        X = X,
                        Y = Y,
                        Z =Z,
                    });
                }

                mat.Dispose();
                log.Info($"完成RGB采样: 方位角{polarLine.Angle}°, 采样点数{polarLine.RgbData.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取数据失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 沿圆周提取RGB数据
        /// </summary>
        private void ExtractRgbAlongCircle(ConcentricCircleLine circleLine, Point center, double radiusAngle, BitmapSource bitmapSource)
        {
            try
            {
                // Convert BitmapSource to OpenCV Mat
                OpenCvSharp.Mat mat = BitmapSourceConverter.ToMat(bitmapSource);

                // Calculate radius in pixels
                double radiusPixels = radiusAngle / ConoscopeConfig.ConoscopeCoefficient;

                // Sample 720 points around the circle for smoother visualization (0.5 degree intervals)
                // Export still uses original data, but display benefits from higher resolution
                int numSamples = 360;
                for (int i = 0; i < numSamples; i++)
                {
                    double anglePos = i * 360.0 / numSamples; // 0.5 degree intervals
                    // Add 90° to place 0° at the top (first quadrant/north)
                    // Negate to make rotation counter-clockwise in screen coordinates
                    double radians = (90 - anglePos) * Math.PI / 180.0;
                    double x = center.X + radiusPixels * Math.Cos(radians);
                    double y = center.Y + radiusPixels * Math.Sin(radians);

                    // Ensure coordinates are within bounds
                    int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                    int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                    // Extract RGB values
                    double r = 0, g = 0, b = 0;
                    double X = 0, Y = 0, Z = 0;

                    ExtractPixelValues(mat, ix, iy, out r, out g, out b, out X, out Y, out Z);

                    circleLine.RgbData.Add(new RgbSample
                    {
                        Position = anglePos, // 0 to 360 with 0.5 degree intervals
                        R = r,
                        G = g,
                        B = b,
                        X = X,
                        Y = Y,
                        Z = Z
                    });
                }

                mat.Dispose();
                log.Info($"完成RGB采样: 极角半径角度{circleLine.RadiusAngle}°, 采样点数{circleLine.RgbData.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取极角数据失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 清除所有方位角
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
                wpfPlotDiameterLine.Plot.Clear();

                if (selectedPolarLine == null || selectedPolarLine.RgbData.Count == 0)
                {
                    wpfPlotDiameterLine.Refresh();
                    return;
                }

                // Extract position and RGB data
                double[] positions = selectedPolarLine.RgbData.Select(s => s.Position).ToArray();

                // Add scatter plots for each channel based on visibility
                if (ConoscopeConfig.IsShowRedChannel)
                {
                    double[] rValues = selectedPolarLine.RgbData.Select(s => s.R).ToArray();
                    var redScatter = wpfPlotDiameterLine.Plot.Add.Scatter(positions, rValues);
                    redScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Red);
                    redScatter.LineWidth = 2;
                    redScatter.LegendText = "R";
                }

                if (ConoscopeConfig.IsShowGreenChannel)
                {
                    double[] gValues = selectedPolarLine.RgbData.Select(s => s.G).ToArray();

                    var greenScatter = wpfPlotDiameterLine.Plot.Add.Scatter(positions, gValues);
                    greenScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Green);
                    greenScatter.LineWidth = 2;
                    greenScatter.LegendText = "G";
                }

                if (ConoscopeConfig.IsShowBlueChannel)
                {
                    double[] bValues = selectedPolarLine.RgbData.Select(s => s.B).ToArray();
                    var blueScatter = wpfPlotDiameterLine.Plot.Add.Scatter(positions, bValues);
                    blueScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Blue);
                    blueScatter.LineWidth = 2;
                    blueScatter.LegendText = "B";
                }
                if (ConoscopeConfig.IsShowXChannel)
                {
                    double[] XValues = selectedPolarLine.RgbData.Select(s => s.X).ToArray();
                    var xScatter = wpfPlotDiameterLine.Plot.Add.Scatter(positions, XValues);
                    xScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gold);
                    xScatter.LineWidth = 2;
                    xScatter.LegendText = "X";
                }
                if (ConoscopeConfig.IsShowYChannel)
                {
                    double[] YValues = selectedPolarLine.RgbData.Select(s => s.Y).ToArray();
                    var yScatter = wpfPlotDiameterLine.Plot.Add.Scatter(positions, YValues);
                    yScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
                    yScatter.LineWidth = 2;
                    yScatter.LegendText = "Y";
                }
                if (ConoscopeConfig.IsShowZChannel)
                {
                    double[] ZValues = selectedPolarLine.RgbData.Select(s => s.Z).ToArray();
                    var zScatter = wpfPlotDiameterLine.Plot.Add.Scatter(positions, ZValues);
                    zScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Violet);
                    zScatter.LineWidth = 2;
                    zScatter.LegendText = "Z";
                }

                wpfPlotDiameterLine.Plot.Title($"方位角 {selectedPolarLine.Angle}°分布曲线");
                wpfPlotDiameterLine.Plot.XLabel("角度 (°)");
                wpfPlotDiameterLine.Plot.YLabel("像素值");
                wpfPlotDiameterLine.Plot.Legend.IsVisible = true;
                wpfPlotDiameterLine.Plot.Axes.AutoScale();

                wpfPlotDiameterLine.Refresh();

                log.Info($"更新图表: 方位角{selectedPolarLine.Angle}°");
            }
            catch (Exception ex)
            {
                log.Error($"更新图表失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 更新ScottPlot显示极角数据
        /// </summary>
        private void UpdatePlotForCircle()
        {
            try
            {
                wpfPlotRCircle.Plot.Clear();

                if (selectedCircleLine == null || selectedCircleLine.RgbData.Count == 0)
                {
                    wpfPlotRCircle.Refresh();
                    return;
                }

                // Extract position (circumferential angle 0-359°) and RGB data
                double[] positions = selectedCircleLine.RgbData.Select(s => s.Position).ToArray();

                // Add scatter plots for each channel based on visibility
                if (ConoscopeConfig.IsShowRedChannel)
                {
                    double[] rValues = selectedCircleLine.RgbData.Select(s => s.R).ToArray();
                    var redScatter = wpfPlotRCircle.Plot.Add.Scatter(positions, rValues);
                    redScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Red);
                    redScatter.LineWidth = 2;
                    redScatter.LegendText = "R";
                }

                if (ConoscopeConfig.IsShowGreenChannel)
                {
                    double[] gValues = selectedCircleLine.RgbData.Select(s => s.G).ToArray();
                    var greenScatter = wpfPlotRCircle.Plot.Add.Scatter(positions, gValues);
                    greenScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Green);
                    greenScatter.LineWidth = 2;
                    greenScatter.LegendText = "G";
                }

                if (ConoscopeConfig.IsShowBlueChannel)
                {
                    double[] bValues = selectedCircleLine.RgbData.Select(s => s.B).ToArray();
                    var blueScatter = wpfPlotRCircle.Plot.Add.Scatter(positions, bValues);
                    blueScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Blue);
                    blueScatter.LineWidth = 2;
                    blueScatter.LegendText = "B";
                }

                if (ConoscopeConfig.IsShowXChannel)
                {
                    double[] XValues = selectedCircleLine.RgbData.Select(s => s.X).ToArray();
                    var xScatter = wpfPlotRCircle.Plot.Add.Scatter(positions, XValues);
                    xScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gold);
                    xScatter.LineWidth = 2;
                    xScatter.LegendText = "X";
                }

                if (ConoscopeConfig.IsShowYChannel)
                {
                    double[] YValues = selectedCircleLine.RgbData.Select(s => s.Y).ToArray();
                    var yScatter = wpfPlotRCircle.Plot.Add.Scatter(positions, YValues);
                    yScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
                    yScatter.LineWidth = 2;
                    yScatter.LegendText = "Y";
                }

                if (ConoscopeConfig.IsShowZChannel)
                {
                    double[] ZValues = selectedCircleLine.RgbData.Select(s => s.Z).ToArray();
                    var zScatter = wpfPlotRCircle.Plot.Add.Scatter(positions, ZValues);
                    zScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Violet);
                    zScatter.LineWidth = 2;
                    zScatter.LegendText = "Z";
                }

                wpfPlotRCircle.Plot.Title($"极角 {selectedCircleLine.RadiusAngle}° 圆周分布曲线");
                wpfPlotRCircle.Plot.XLabel("圆周角度 (°)");
                wpfPlotRCircle.Plot.YLabel("像素值");
                wpfPlotRCircle.Plot.Legend.IsVisible = true;
                wpfPlotRCircle.Plot.Axes.AutoScale();

                wpfPlotRCircle.Refresh();

                log.Info($"更新图表: 极角半径角度{selectedCircleLine.RadiusAngle}°");
            }
            catch (Exception ex)
            {
                log.Error($"更新极角图表失败: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            FlowEngineManager.GetInstance().BatchRecord -= ConoscopeWindow_BatchRecord;

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

        private void RibbonButton_Click_1(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            FlowEngineManager.GetInstance().DisplayFlow.RunFlow();
        }

        /// <summary>
        /// 高级导出按钮点击事件
        /// </summary>
        private void btnAdvancedExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new AdvancedExportDialog { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    var settings = dialog.Settings;
                    PerformAdvancedExport(settings);
                }
            }
            catch (Exception ex)
            {
                log.Error($"高级导出失败: {ex.Message}", ex);
                MessageBox.Show($"高级导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行高级导出
        /// </summary>
        private void PerformAdvancedExport(AdvancedExportSettings settings)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                int filesExported = 0;

                // Select output folder
                using (var folderDialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    folderDialog.Description = "选择导出文件夹";
                    folderDialog.ShowNewFolderButton = true;

                    if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    {
                        return; // User cancelled
                    }

                    string outputFolder = folderDialog.SelectedPath;

                    // Handle cross-section export separately
                    if (settings.EnableCrossSection)
                    {
                        ExportCrossSectionToFolder(settings, timestamp, outputFolder, ref filesExported);
                        MessageBox.Show($"截面导出完成，共导出 {filesExported} 个文件到:\n{outputFolder}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        log.Info($"截面导出完成: {filesExported} 个文件");
                        return;
                    }

                    // Export azimuth data
                    if (settings.ExportAzimuth)
                    {
                        foreach (var channel in settings.Channels)
                        {
                            string filename = $"{settings.FilePrefix}_Azimuth_{channel}_{timestamp}.csv";
                            string filePath = Path.Combine(outputFolder, filename);
                            ExportAzimuthWithStep(filePath, channel, settings.AzimuthStep, settings.RadialStep);
                            filesExported++;
                            log.Info($"方位角导出成功: {filePath}");
                        }
                    }

                    // Export polar data
                    if (settings.ExportPolar)
                    {
                        foreach (var channel in settings.Channels)
                        {
                            string filename = $"{settings.FilePrefix}_Polar_{channel}_{ConoscopeConfig.CurrentModel}_{timestamp}.csv";
                            string filePath = Path.Combine(outputFolder, filename);
                            ExportPolarWithStep(filePath, channel, settings.PolarStep, settings.CircumferentialStep);
                            filesExported++;
                            log.Info($"极角导出成功: {filePath}");
                        }
                    }

                    MessageBox.Show($"导出完成，共导出 {filesExported} 个文件到:\n{outputFolder}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"高级导出完成: {filesExported} 个文件");
                }
            }
            catch (Exception ex)
            {
                log.Error($"高级导出执行失败: {ex.Message}", ex);
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出截面数据到文件夹
        /// </summary>
        private void ExportCrossSectionToFolder(AdvancedExportSettings settings, string timestamp, string outputFolder, ref int filesExported)
        {
            try
            {
                foreach (var channel in settings.Channels)
                {
                    string sectionType = settings.CrossSectionType == CrossSectionType.Azimuth ? "Azimuth" : "Polar";
                    string filename = $"{settings.FilePrefix}_CrossSection_{sectionType}_{settings.CrossSectionAngle}deg_{channel}_{timestamp}.csv";
                    string filePath = Path.Combine(outputFolder, filename);
                    
                    if (settings.CrossSectionType == CrossSectionType.Azimuth)
                    {
                        ExportAzimuthCrossSection(filePath, channel, settings.CrossSectionAngle);
                    }
                    else
                    {
                        ExportPolarCrossSection(filePath, channel, settings.CrossSectionAngle);
                    }
                    filesExported++;
                    log.Info($"截面导出成功: {filePath}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"截面导出失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 按步进导出方位角数据
        /// </summary>
        private void ExportAzimuthWithStep(string filePath, ExportChannel channel, double azimuthStep, double radialStep)
        {
            if (currentBitmapSource == null)
            {
                log.Warn("没有图像数据，无法导出");
                return;
            }

            OpenCvSharp.Mat mat = BitmapSourceConverter.ToMat(currentBitmapSource);

            try
            {
                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // Write header comments
                    writer.WriteLine($"# Azimuth Export Data (azimuth step = {azimuthStep}°, radial step = {radialStep}°)");
                    writer.WriteLine($"# Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"# Export Channel: {channel}");
                    writer.WriteLine($"# Model: {ConoscopeConfig.CurrentModel}");
                    writer.WriteLine($"# Max Angle: {MaxAngle}°");
                    writer.WriteLine($"# Phi (Column): Azimuth angle (0°-180°, step={azimuthStep}°)");
                    writer.WriteLine($"# Theta (Row): Polar radius (0 to MaxAngle, step={radialStep}°)");
                    writer.WriteLine();

                    // Create list of angles to export based on step
                    var anglesToExport = new List<double>();
                    for (double phi = 0; phi <= 180; phi += azimuthStep)
                    {
                        anglesToExport.Add(phi);
                    }

                    // Write CSV header
                    StringBuilder headerLine = new StringBuilder();
                    headerLine.Append("Phi \\ Theta");
                    foreach (var angle in anglesToExport)
                    {
                        headerLine.Append($",{angle:F2}");
                    }
                    writer.WriteLine(headerLine.ToString());

                    // Sample data for each angle
                    var angleData = new List<List<RgbSample>>();
                    foreach (var phi in anglesToExport)
                    {
                        var samples = new List<RgbSample>();
                        // Add 90° to place 0° at the top (first quadrant/north)
                        // Negate to make rotation counter-clockwise in screen coordinates
                        double radians = (90 - phi) * Math.PI / 180.0;

                        for (double theta = 0; theta <= MaxAngle; theta += radialStep)
                        {
                            double radiusPixels = theta / ConoscopeConfig.ConoscopeCoefficient;
                            double x = currentImageCenter.X + radiusPixels * Math.Cos(radians);
                            double y = currentImageCenter.Y + radiusPixels * Math.Sin(radians);

                            int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                            int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                            double r = 0, g = 0, b = 0;
                            double X = 0, Y = 0, Z = 0;

                            ExtractPixelValues(mat, ix, iy, out r, out g, out b, out X, out Y, out Z);

                            samples.Add(new RgbSample
                            {
                                Position = theta,
                                R = r,
                                G = g,
                                B = b,
                                X = X,
                                Y = Y,
                                Z = Z
                            });
                        }
                        angleData.Add(samples);
                    }

                    // Write data rows
                    int maxSamples = angleData[0].Count;
                    for (int i = 0; i < maxSamples; i++)
                    {
                        StringBuilder dataLine = new StringBuilder();
                        dataLine.Append($"{angleData[0][i].Position:F2}");

                        foreach (var samples in angleData)
                        {
                            if (samples.Count > i)
                            {
                                double value = GetChannelValue(samples[i], channel);
                                dataLine.Append($",{value:F2}");
                            }
                            else
                            {
                                dataLine.Append(",");
                            }
                        }
                        writer.WriteLine(dataLine.ToString());
                    }

                    log.Info($"方位角导出完成，步进={azimuthStep}, 角度数={anglesToExport.Count}, 通道={channel}");
                }
            }
            finally
            {
                mat.Dispose();
            }
        }

        /// <summary>
        /// 按步进导出极角数据
        /// </summary>
        private void ExportPolarWithStep(string filePath, ExportChannel channel, double polarStep, double circumStep)
        {
            if (currentBitmapSource == null)
            {
                log.Warn("没有图像数据，无法导出");
                return;
            }

            OpenCvSharp.Mat mat = BitmapSourceConverter.ToMat(currentBitmapSource);

            try
            {
                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // Write header comments
                    writer.WriteLine($"# Polar Angle Export Data (ring step = {polarStep}°, circumferential step = {circumStep}°)");
                    writer.WriteLine($"# Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"# Export Channel: {channel}");
                    writer.WriteLine($"# Model: {ConoscopeConfig.CurrentModel}");
                    writer.WriteLine($"# Max Angle: {MaxAngle}°");
                    writer.WriteLine($"# Phi (Column): Polar radius angle (0-{MaxAngle}°, step={polarStep}°)");
                    writer.WriteLine($"# Theta (Row): Circumferential angle (0-360°, step={circumStep}°)");
                    writer.WriteLine();

                    // Create list of polar angles to export
                    var polarAngles = new List<double>();
                    for (double phi = 0; phi <= MaxAngle; phi += polarStep)
                    {
                        polarAngles.Add(phi);
                    }

                    // Write CSV header
                    StringBuilder headerLine = new StringBuilder();
                    headerLine.Append("Phi \\ Theta");
                    foreach (var angle in polarAngles)
                    {
                        headerLine.Append($",{angle:F2}");
                    }
                    writer.WriteLine(headerLine.ToString());

                    // Sample data for each polar angle
                    var polarData = new List<List<RgbSample>>();
                    foreach (var polarAngle in polarAngles)
                    {
                        var samples = new List<RgbSample>();
                        double radiusPixels = polarAngle / ConoscopeConfig.ConoscopeCoefficient;

                        for (double theta = 0; theta <= 360; theta += circumStep)
                        {
                            // Add 90° to place 0° at the top (first quadrant/north)
                            // Negate to make rotation counter-clockwise in screen coordinates
                            double radians = (90 - theta) * Math.PI / 180.0;
                            double x = currentImageCenter.X + radiusPixels * Math.Cos(radians);
                            double y = currentImageCenter.Y + radiusPixels * Math.Sin(radians);

                            int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                            int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                            double r = 0, g = 0, b = 0;
                            double X = 0, Y = 0, Z = 0;

                            ExtractPixelValues(mat, ix, iy, out r, out g, out b, out X, out Y, out Z);

                            samples.Add(new RgbSample
                            {
                                Position = theta,
                                R = r,
                                G = g,
                                B = b,
                                X = X,
                                Y = Y,
                                Z = Z
                            });
                        }
                        polarData.Add(samples);
                    }

                    // Write data rows
                    int maxSamples = polarData[0].Count;
                    for (int i = 0; i < maxSamples; i++)
                    {
                        StringBuilder dataLine = new StringBuilder();
                        dataLine.Append($"{polarData[0][i].Position:F2}");

                        foreach (var samples in polarData)
                        {
                            if (samples.Count > i)
                            {
                                double value = GetChannelValue(samples[i], channel);
                                dataLine.Append($",{value:F2}");
                            }
                            else
                            {
                                dataLine.Append(",");
                            }
                        }
                        writer.WriteLine(dataLine.ToString());
                    }

                    log.Info($"极角导出完成，极角步进={polarStep}, 圆周步进={circumStep}, 极角数={polarAngles.Count}, 通道={channel}");
                }
            }
            finally
            {
                mat.Dispose();
            }
        }

        /// <summary>
        /// 导出方位角截面数据
        /// </summary>
        private void ExportAzimuthCrossSection(string filePath, ExportChannel channel, double azimuthAngle)
        {
            if (currentBitmapSource == null)
            {
                log.Warn("没有图像数据，无法导出");
                return;
            }

            OpenCvSharp.Mat mat = BitmapSourceConverter.ToMat(currentBitmapSource);

            try
            {
                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // Write header comments
                    writer.WriteLine($"# Azimuth Cross-Section Export (Angle = {azimuthAngle}°)");
                    writer.WriteLine($"# Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"# Export Channel: {channel}");
                    writer.WriteLine($"# Model: {ConoscopeConfig.CurrentModel}");
                    writer.WriteLine($"# Max Angle: {MaxAngle}°");
                    writer.WriteLine();

                    // Write CSV header
                    writer.WriteLine("Polar Radius (degrees),Value");

                    // Add 90° to place 0° at the top (first quadrant/north)
                    // Negate to make rotation counter-clockwise in screen coordinates
                    double radians = (90 - azimuthAngle) * Math.PI / 180.0;

                    // Sample from center to edge
                    for (int theta = -(int)MaxAngle; theta <= (int)MaxAngle; theta++)
                    {
                        double radiusPixels = theta / ConoscopeConfig.ConoscopeCoefficient;
                        double x = currentImageCenter.X + radiusPixels * Math.Cos(radians);
                        double y = currentImageCenter.Y + radiusPixels * Math.Sin(radians);

                        int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                        int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                        double r = 0, g = 0, b = 0;
                        double X = 0, Y = 0, Z = 0;

                        ExtractPixelValues(mat, ix, iy, out r, out g, out b, out X, out Y, out Z);

                        var sample = new RgbSample { R = r, G = g, B = b, X = X, Y = Y, Z = Z };
                        double value = GetChannelValue(sample, channel);
                        
                        writer.WriteLine($"{theta},{value:F2}");
                    }

                    log.Info($"方位角截面导出完成: {azimuthAngle}°, 通道={channel}");
                }
            }
            finally
            {
                mat.Dispose();
            }
        }

        /// <summary>
        /// 导出极角截面数据
        /// </summary>
        private void ExportPolarCrossSection(string filePath, ExportChannel channel, double polarAngle)
        {
            if (currentBitmapSource == null)
            {
                log.Warn("没有图像数据，无法导出");
                return;
            }

            OpenCvSharp.Mat mat = BitmapSourceConverter.ToMat(currentBitmapSource);

            try
            {
                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // Write header comments
                    writer.WriteLine($"# Polar Cross-Section Export (Radius Angle = {polarAngle}°)");
                    writer.WriteLine($"# Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"# Export Channel: {channel}");
                    writer.WriteLine($"# Model: {ConoscopeConfig.CurrentModel}");
                    writer.WriteLine($"# Max Angle: {MaxAngle}°");
                    writer.WriteLine();

                    // Write CSV header
                    writer.WriteLine("Circumferential Angle (degrees),Value");

                    double radiusPixels = polarAngle / ConoscopeConfig.ConoscopeCoefficient;

                    // Sample around the circle
                    for (int theta = 0; theta <= 360; theta++)
                    {
                        // Add 90° to place 0° at the top (first quadrant/north)
                        // Negate to make rotation counter-clockwise in screen coordinates
                        double radians = (90 - theta) * Math.PI / 180.0;
                        double x = currentImageCenter.X + radiusPixels * Math.Cos(radians);
                        double y = currentImageCenter.Y + radiusPixels * Math.Sin(radians);

                        int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                        int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                        double r = 0, g = 0, b = 0;
                        double X = 0, Y = 0, Z = 0;

                        ExtractPixelValues(mat, ix, iy, out r, out g, out b, out X, out Y, out Z);

                        var sample = new RgbSample { R = r, G = g, B = b, X = X, Y = Y, Z = Z };
                        double value = GetChannelValue(sample, channel);
                        
                        writer.WriteLine($"{theta},{value:F2}");
                    }

                    log.Info($"极角截面导出完成: {polarAngle}°, 通道={channel}");
                }
            }
            finally
            {
                mat.Dispose();
            }
        }

        /// <summary>
        /// 导出当前选中的方位角
        /// </summary>
        private void btnExportCurrentAzimuth_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedPolarLine == null)
                {
                    MessageBox.Show("请先选择一个方位角", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get channel selection
                var channel = GetSelectedExportChannel();
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"Azimuth_{selectedPolarLine.Angle}deg_{channel}_{timestamp}.csv";

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = filename,
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportAzimuthCrossSection(saveFileDialog.FileName, channel, selectedPolarLine.Angle);
                    MessageBox.Show($"方位角 {selectedPolarLine.Angle}° 导出成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"单个方位角导出成功: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"导出当前方位角失败: {ex.Message}", ex);
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出当前选中的极角
        /// </summary>
        private void btnExportCurrentPolar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedCircleLine == null)
                {
                    MessageBox.Show("请先选择一个极角", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get channel selection
                var channel = GetSelectedExportChannel();
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"Polar_{selectedCircleLine.RadiusAngle}deg_{channel}_{timestamp}.csv";

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = filename,
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportPolarCrossSection(saveFileDialog.FileName, channel, selectedCircleLine.RadiusAngle);
                    MessageBox.Show($"极角 {selectedCircleLine.RadiusAngle}° 导出成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"单个极角导出成功: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"导出当前极角失败: {ex.Message}", ex);
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
