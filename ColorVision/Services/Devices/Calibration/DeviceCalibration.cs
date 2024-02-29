using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera;
using System;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Calibration
{
    public class DeviceCalibration : DeviceService<ConfigCalibration>
    {
        public MQTTCalibration DeviceService { get; set; }

        public DeviceCalibration(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTCalibration(Config);
            EditLazy = new Lazy<EditCalibration>(() => { EditCalibration ??= new EditCalibration(this); return EditCalibration; });
        }

        public override UserControl GetDeviceControl() => new DeviceCalibrationControl(this);

        public override UserControl GetDeviceInfo() => new DeviceCalibrationControl(this,false);

        public override UserControl GetDisplayControl() => new DisplayCalibrationControl(this);



        readonly Lazy<EditCalibration> EditLazy;
        public EditCalibration EditCalibration { get; set; }
        public override UserControl GetEditControl() => EditLazy.Value;


    }
}
