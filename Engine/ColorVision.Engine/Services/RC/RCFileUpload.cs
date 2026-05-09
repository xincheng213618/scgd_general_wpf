using ColorVision.Engine.Messages;

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
