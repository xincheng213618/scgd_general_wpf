using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using NPOI.SS.Formula.Functions;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Calibration
{
    public class DeviceCalibration : BaseDevice<ConfigCalibration>
    {
        public DeviceServiceCalibration DeviceService { get; set; }

        public DeviceCalibration(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new DeviceServiceCalibration(Config);
        }

        public override UserControl GetDeviceControl() => new DeviceCalibrationControl(this);

        public override UserControl GetDeviceInfo() => new DeviceCalibrationControl(this,false);

        public override UserControl GetDisplayControl() => new DisplayCalibrationControl(this);


    }
}
