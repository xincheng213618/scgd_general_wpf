﻿using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Sensor
{
    public class DeviceSensor : DeviceService<ConfigSensor>
    {
        public MQTTSensor DeviceService { get; set; }

        public DeviceSensor(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTSensor(Config);
        }

        public override UserControl GetDeviceControl() => new DeviceSensorControl(this);
        public override UserControl GetDeviceInfo() => new DeviceSensorControl(this, false);
        public override UserControl GetDisplayControl() => new DisplaySensorControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
    }
}
