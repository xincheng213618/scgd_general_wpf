using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Dao;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Templates;
using ColorVision.ImageEditor;
using ColorVision.UI.LogImp;
using ColorVision.UI.Menus;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

        private DeviceCamera? selectedObservationCamera;
        private DeviceCamera? selectedMeasurementCamera;

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
            
            cbObservationCamera.ItemsSource = cameras;
            cbObservationCamera.DisplayMemberPath = "Name";
            if (cameras.Count > 0)
                cbObservationCamera.SelectedIndex = 0;

            cbMeasurementCamera.ItemsSource = cameras;
            cbMeasurementCamera.DisplayMemberPath = "Name";
            if (cameras.Count > 0)
                cbMeasurementCamera.SelectedIndex = 0;

            log.Info($"已加载 {cameras.Count} 个相机服务");
        }

        private void cbObservationCamera_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedObservationCamera = cbObservationCamera.SelectedItem as DeviceCamera;
            if (selectedObservationCamera != null)
            {
                log.Info($"已选择观察相机: {selectedObservationCamera.Name}");
            }
        }

        private void cbMeasurementCamera_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedMeasurementCamera = cbMeasurementCamera.SelectedItem as DeviceCamera;
            if (selectedMeasurementCamera != null)
            {
                log.Info($"已选择测量相机: {selectedMeasurementCamera.Name}");
                
                // Load calibration templates for selected camera
                LoadCalibrationTemplates();
            }
        }

        private void LoadCalibrationTemplates()
        {
            if (selectedMeasurementCamera?.PhyCamera == null)
            {
                cbCalibration.ItemsSource = null;
                cbCalibration.SelectedIndex = -1;
                return;
            }

            var templates = CalibrationDao.Instance.GetAllByPid(selectedMeasurementCamera.PhyCamera.Id);
            var calibrationList = new List<CalibrationParam> { new CalibrationParam { Id = -1, Name = "无校正" } };
            calibrationList.AddRange(templates);
            
            cbCalibration.ItemsSource = calibrationList;
            cbCalibration.DisplayMemberPath = "Name";
            cbCalibration.SelectedIndex = 0;
            
            log.Info($"已加载 {templates.Count} 个校正模板");
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
                    // VA60: 显示观察相机控件
                    ObservationCameraPanel.Visibility = Visibility.Visible;
                    tbModelDescription.Text = "VA60: 一台观察相机（视频模式）+ 一台测量相机（需要校正）";
                    if (tbObservationCameraStatus != null)
                    {
                        tbObservationCameraStatus.Visibility = Visibility.Visible;
                    }
                    break;
                    
                case ConoscopeModelType.VA80:
                    // VA80: 隐藏观察相机控件
                    ObservationCameraPanel.Visibility = Visibility.Collapsed;
                    tbModelDescription.Text = "VA80: 一台测量相机（需要校正）";
                    if (tbObservationCameraStatus != null)
                    {
                        tbObservationCameraStatus.Visibility = Visibility.Collapsed;
                    }
                    break;
            }
        }

        private void btnOpenObservationCamera_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedObservationCamera == null)
                {
                    MessageBox.Show("请先选择观察相机", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (observationCameraWindow == null || !observationCameraWindow.IsVisible)
                {
                    observationCameraWindow = new MVSViewWindow();
                    observationCameraWindow.Title = $"观察相机 - {selectedObservationCamera.Name}";
                    observationCameraWindow.Closed += (s, args) =>
                    {
                        tbObservationCameraStatus.Text = "已关闭";
                        tbObservationCameraStatus.Foreground = new SolidColorBrush(Colors.Gray);
                        log.Info("观察相机窗口已关闭");
                    };
                    observationCameraWindow.Show();
                    
                    tbObservationCameraStatus.Text = "已打开";
                    tbObservationCameraStatus.Foreground = new SolidColorBrush(Colors.Green);
                    log.Info($"观察相机窗口已打开: {selectedObservationCamera.Name}");
                }
                else
                {
                    observationCameraWindow.Activate();
                    log.Info("观察相机窗口已激活");
                }
            }
            catch (Exception ex)
            {
                log.Error($"打开观察相机失败: {ex.Message}", ex);
                MessageBox.Show($"打开观察相机失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnOpenMeasurementCamera_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedMeasurementCamera == null)
                {
                    MessageBox.Show("请先选择测量相机", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (cbCalibration.SelectedItem == null)
                {
                    MessageBox.Show("请先选择校正模板", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var calibrationParam = cbCalibration.SelectedItem as CalibrationParam;
                if (calibrationParam == null)
                {
                    MessageBox.Show("校正参数无效", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                log.Info($"准备获取图像 - 相机: {selectedMeasurementCamera.Name}, 校正: {calibrationParam.Name}");
                
                double[] expTime = new double[] { selectedMeasurementCamera.Config.ExpTime };
                AutoExpTimeParam autoExpTimeParam = new AutoExpTimeParam { Id = -1 };
                ParamBase hdrParam = new ParamBase { Id = -1 };

                MsgRecord msgRecord = selectedMeasurementCamera.DService.GetData(expTime, calibrationParam, autoExpTimeParam, hdrParam);

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

                    msgRecord.MsgFailed += (arg) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            tbMeasurementCameraStatus.Text = "失败";
                            tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Red);
                            MessageBox.Show($"获取图像失败: {arg.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            log.Error($"获取图像失败: {arg.Message}");
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

        public void Dispose()
        {
            logOutput?.Dispose();
            ImageView?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
