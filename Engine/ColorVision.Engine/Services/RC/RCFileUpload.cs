using ColorVision.Common.Utilities;
using ColorVision.Engine.Messages;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.RC
{
    public class RCFileUpload: MQTTServiceBase
    {
        private static RCFileUpload _instance;
        private static readonly object _locker = new();
        public static RCFileUpload GetInstance() { lock (_locker) { return _instance ??= new RCFileUpload(); } }

        public MsgRecord CreatePhysicalCameraFloder(string cameraID)
        {
            MsgSend msg = new()
            {
                DeviceCode = cameraID,
                EventName = "PhysicalCamera_Load",
            };
            return PublishAsyncClient(msg); 
        }

    }
}
