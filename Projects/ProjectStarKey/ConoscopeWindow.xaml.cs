using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ProjectStarKey
{
    /// <summary>
    /// ConoscopeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ConoscopeWindow : Window
    {
        private string currentModel = "VA60";
        private MVSViewWindow? observationCameraWindow;
        private MVSViewWindow? measurementCameraWindow;

        public ConoscopeWindow()
        {
            InitializeComponent();
            UpdateUIForModel(currentModel);
            LogMessage("视角测量窗口已初始化");
        }

        private void cbModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbModelType.SelectedItem is ComboBoxItem selectedItem)
            {
                string? modelTag = selectedItem.Tag as string;
                if (modelTag != null)
                {
                    currentModel = modelTag;
                    UpdateUIForModel(currentModel);
                    LogMessage($"已切换到型号: {currentModel}");
                }
            }
        }

        private void UpdateUIForModel(string model)
        {
            tbCurrentModel.Text = model;

            if (model == "VA60")
            {
                // VA60: 显示观察相机按钮
                btnOpenObservationCamera.Visibility = Visibility.Visible;
                tbModelDescription.Text = "VA60: 一台观察相机（视频模式）+ 一台测量相机（需要校正）";
            }
            else if (model == "VA80")
            {
                // VA80: 隐藏观察相机按钮
                btnOpenObservationCamera.Visibility = Visibility.Collapsed;
                tbModelDescription.Text = "VA80: 一台测量相机（需要校正）";
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
                        LogMessage("观察相机窗口已关闭");
                    };
                    observationCameraWindow.Show();
                    
                    tbObservationCameraStatus.Text = "已打开";
                    tbObservationCameraStatus.Foreground = new SolidColorBrush(Colors.Green);
                    LogMessage("观察相机窗口已打开");
                }
                else
                {
                    observationCameraWindow.Activate();
                    LogMessage("观察相机窗口已激活");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"打开观察相机失败: {ex.Message}");
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
                        LogMessage("测量相机窗口已关闭");
                    };
                    measurementCameraWindow.Show();
                    
                    tbMeasurementCameraStatus.Text = "已打开";
                    tbMeasurementCameraStatus.Foreground = new SolidColorBrush(Colors.Green);
                    LogMessage("测量相机窗口已打开");
                }
                else
                {
                    measurementCameraWindow.Activate();
                    LogMessage("测量相机窗口已激活");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"打开测量相机失败: {ex.Message}");
                MessageBox.Show($"打开测量相机失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCalibration_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for calibration functionality
            // This will be implemented in future iterations
            LogMessage("校正功能将在后续版本中实现");
            MessageBox.Show("校正功能将在后续版本中实现。\n\n这里将实现测量相机的常规校正流程。", 
                "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}\n";
            
            if (tbLog.Text == "系统日志将显示在此处...")
            {
                tbLog.Text = logEntry;
            }
            else
            {
                tbLog.Text += logEntry;
            }
        }
    }
}
