using ColorVision.Services.Dao;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Calibration
{
    public class DeviceCalibration : DeviceService<ConfigCalibration>
    {
        public MQTTCalibration DeviceService { get; set; }

        public DeviceCalibration(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTCalibration(Config);
        }

        public override UserControl GetDeviceControl() => new DeviceCalibrationControl(this);

        public override UserControl GetDeviceInfo() => new DeviceCalibrationControl(this,false);

        public override UserControl GetDisplayControl() => new DisplayCalibrationControl(this);


    }
}
