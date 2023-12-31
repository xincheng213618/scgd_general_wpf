﻿using ColorVision.Services.Device;
using ColorVision.Services.Device.Camera;

namespace ColorVision.Services.Algorithm
{
    public class ConfigAlgorithm : BaseDeviceConfig, IServiceConfig
    {
        public string BindDeviceCode { get => _BindDeviceCode; set { _BindDeviceCode = value; NotifyPropertyChanged(); } }
        private string _BindDeviceCode;
        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
