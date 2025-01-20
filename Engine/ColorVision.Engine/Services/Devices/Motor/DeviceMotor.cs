using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Motor
{
    public class DeviceMotor : DeviceService<ConfigMotor>,IIcon
    {
        public MQTTMotor DService { get; set; }

        public DeviceMotor(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTMotor(Config);
            this.SetIconResource("COMDrawingImage");
          
            EditCommand = new RelayCommand(a =>
            {
                EditMotor window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceInfo() => new InfoMotor(this);

        public override UserControl GetDisplayControl() => new DisplayMotorControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
