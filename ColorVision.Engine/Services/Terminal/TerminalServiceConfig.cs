﻿using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Types;

namespace ColorVision.Engine.Services.Terminal
{
    public class TerminalServiceConfig : BaseConfig, IServiceConfig, IHeartbeat
    {
        public ServiceTypes ServiceType { get; set; }
    }
}
