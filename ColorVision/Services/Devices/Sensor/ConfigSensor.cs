﻿using ColorVision.Services.Devices.PG;

namespace ColorVision.Services.Devices.Sensor
{
    public class ConfigSensor : DeviceServiceConfig
    {
        public CommunicateType CommunicateType { get => _CommunicateType; set { _CommunicateType = value; NotifyPropertyChanged(); } }
        private CommunicateType _CommunicateType;

        public string SzIPAddress { get => _szIPAddress; set { _szIPAddress = value; NotifyPropertyChanged(); } }
        private string _szIPAddress;

        public uint Port { get => _Port; set { _Port = value; NotifyPropertyChanged(); } }
        private uint _Port;

    }
}
