using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Sensor
{
    public class DeviceSensor : DeviceService<ConfigSensor>
    {
        public MQTTSensor DeviceService { get; set; }

        public DeviceSensor(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTSensor(Config);
            EditCommand = new RelayCommand(a =>
            {
                EditSensor window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceControl() => new InfoSensor(this);
        public override UserControl GetDeviceInfo() => new InfoSensor(this);
        public override UserControl GetDisplayControl() => new DisplaySensor(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
    }
}
