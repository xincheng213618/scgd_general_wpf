using ColorVision.Common.Utilities;
using ColorVision.Services.PhyCameras;
using ColorVision.Services.PhyCameras.Templates;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Calibration
{
    /// <summary>
    /// InfoSMU.xaml 的交互逻辑
    /// </summary>
    public partial class InfoCalibration : UserControl
    {
        public DeviceCalibration Device { get; set; }

        public MQTTCalibration DService { get => Device.DeviceService; }
        public ServiceManager ServiceControl { get; set; }

        public bool IsCanEdit { get; set; }

        public InfoCalibration(DeviceCalibration deviceCalibration, bool isCanEdit = true)
        {
            Device = deviceCalibration;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
        }


        private void ServiceCache_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "文件删除后不可找回", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    var MsgRecord = DService.CacheClear();
                    MsgRecord.MsgSucessed += (s) =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "文件服务清理完成", "ColorVison");
                        MsgRecord.ClearMsgRecordSucessChangedHandler();
                    };
                    ServicesHelper.SendCommand(button, MsgRecord);
                }
            }
        }
    }
}
