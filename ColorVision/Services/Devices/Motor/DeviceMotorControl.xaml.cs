using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Motor
{
    /// <summary>
    /// DeviceSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceMotorControl : UserControl, IDisposable
    {
        public DeviceMotor Device { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public bool IsCanEdit { get; set; }
        public DeviceMotorControl(DeviceMotor deviceMotor, bool isCanEdit = true)
        {
            Device = deviceMotor;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            DataContext = Device;
            List<string> Serials = new List<string> { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" };
            TextSerial.ItemsSource = Serials;
            List<int> BaudRates = new List<int> { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 };
            TextBaudRate.ItemsSource = BaudRates;

            ComboxeEvaFunc.ItemsSource = from e1 in Enum.GetValues(typeof(EvaFunc)).Cast<EvaFunc>()
                                         select new KeyValuePair<EvaFunc, string>(e1, e1.ToString());

            ComboxMotorType.ItemsSource = from e1 in Enum.GetValues(typeof(FOCUS_COMMUN)).Cast<FOCUS_COMMUN>()
                                          select new KeyValuePair<FOCUS_COMMUN, string>(e1, e1.ToString());
            int index = 0;
            ComboxMotorType.SelectionChanged += (s, e) =>
            {
                if (index++ < 1)
                    return;
                switch (Device.Config.eFOCUSCOMMUN)
                {
                    case FOCUS_COMMUN.VID_SERIAL:
                        Device.Config.BaudRate = 115200;
                        break;
                    case FOCUS_COMMUN.CANON_SERIAL:
                        Device.Config.BaudRate = 38400;
                        break;
                    case FOCUS_COMMUN.NED_SERIAL:
                        Device.Config.BaudRate = 115200;
                        break;
                    case FOCUS_COMMUN.LONGFOOT_SERIAL:
                        Device.Config.BaudRate = 115200;
                        break;
                    default:
                        break;
                }
            };
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

    }
}
