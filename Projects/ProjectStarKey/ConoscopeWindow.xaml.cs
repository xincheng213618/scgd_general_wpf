using ColorVision.ImageEditor;
using ColorVision.UI.LogImp;
using ColorVision.UI.Menus;
using log4net;
using System;
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
        private MVSViewWindow? measurementCameraWindow;
        private LogOutput? logOutput;

        public ConoscopeWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            // Initialize LogOutput control
            logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
            LogGrid.Children.Add(logOutput);

            // Initialize UI
            UpdateUIForModel(currentModel);
            log.Info("视角测量窗口已初始化");

            this.Closed += (s, e) =>
            {
                this.Dispose();
            };
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
                    // VA60: 显示观察相机按钮
                    btnOpenObservationCamera.Visibility = Visibility.Visible;
                    tbModelDescription.Text = "VA60: 一台观察相机（视频模式）+ 一台测量相机（需要校正）";
                    if (tbObservationCameraStatus != null)
                    {
                        tbObservationCameraStatus.Visibility = Visibility.Visible;
                    }
                    break;
                    
                case ConoscopeModelType.VA80:
                    // VA80: 隐藏观察相机按钮
                    btnOpenObservationCamera.Visibility = Visibility.Collapsed;
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
                if (observationCameraWindow == null || !observationCameraWindow.IsVisible)
                {
                    observationCameraWindow = new MVSViewWindow();
                    observationCameraWindow.Title = "观察相机 - 视频模式";
                    observationCameraWindow.Closed += (s, args) =>
                    {
                        tbObservationCameraStatus.Text = "已关闭";
                        tbObservationCameraStatus.Foreground = new SolidColorBrush(Colors.Gray);
                        log.Info("观察相机窗口已关闭");
                    };
                    observationCameraWindow.Show();
                    
                    tbObservationCameraStatus.Text = "已打开";
                    tbObservationCameraStatus.Foreground = new SolidColorBrush(Colors.Green);
                    log.Info("观察相机窗口已打开");
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
                if (measurementCameraWindow == null || !measurementCameraWindow.IsVisible)
                {
                    measurementCameraWindow = new MVSViewWindow();
                    measurementCameraWindow.Title = "测量相机";
                    measurementCameraWindow.Closed += (s, args) =>
                    {
                        tbMeasurementCameraStatus.Text = "已关闭";
                        tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Gray);
                        log.Info("测量相机窗口已关闭");
                    };
                    measurementCameraWindow.Show();
                    
                    tbMeasurementCameraStatus.Text = "已打开";
                    tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Green);
                    log.Info("测量相机窗口已打开");
                }
                else
                {
                    measurementCameraWindow.Activate();
                    log.Info("测量相机窗口已激活");
                }
            }
            catch (Exception ex)
            {
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
