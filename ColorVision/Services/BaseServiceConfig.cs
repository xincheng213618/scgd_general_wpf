﻿using ColorVision.Services.Device;

namespace ColorVision.Services
{
    public class BaseServiceConfig : BaseConfig, IServiceConfig, IHeartbeat
    {
        public ServiceType ServiceType { get; set; }


    }


}
