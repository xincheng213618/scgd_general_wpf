using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Motor
{
    /// <summary>
    /// InfoSMU.xaml 的交互逻辑
    /// </summary>
    public partial class InfoMotor : UserControl, IDisposable
    {
        public DeviceMotor Device { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public bool IsCanEdit { get; set; }
        public InfoMotor(DeviceMotor deviceMotor, bool isCanEdit = true)
        {
            Device = deviceMotor;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            DataContext = Device;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

    }
}
