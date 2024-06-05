using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Util.Interfaces;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.Motor
{
    public class DeviceMotor : DeviceService<ConfigMotor>,IIcon
    {
        public MQTTMotor DeviceService { get; set; }

        public DeviceMotor(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTMotor(Config);
            this.SetIconResource("COMDrawingImage");
          
            EditCommand = new RelayCommand(a =>
            {
                EditMotor window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });
        }

        public override UserControl GetDeviceControl() => new InfoMotor(this);
        public override UserControl GetDeviceInfo() => new InfoMotor(this, false);

        public override UserControl GetDisplayControl() => new DisplayMotorControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
    }
}
