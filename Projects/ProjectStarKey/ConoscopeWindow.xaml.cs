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

namespace ProjectStarKey
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
            log.Info("视角测量窗口已初始化");

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
                    ObservationCameraPanel.Visibility = Visibility.Visible;
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
                    ObservationCameraPanel.Visibility = Visibility.Collapsed;
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
                                            tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Green);
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
    }
}
