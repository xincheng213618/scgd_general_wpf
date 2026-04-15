using ColorVision.SocketProtocol;
using log4net;
using Newtonsoft.Json;
using System.Net.Sockets;

namespace ProjectARVRPro.Services
{
    /// <summary>
    /// 处理外部Client返回的AOITestSwitchImageComplete事件
    /// 外部Client完成切图后回传此消息，我们将其转发给Flow (通过SocketRelayManager)
    /// 
    /// 完整流程：
    /// Flow → SocketRelayServer → SocketControl.Current.Stream → 外部Client
    /// 外部Client → SocketManager → AOITestSwitchImageCompleteHandler → SocketRelayServer → Flow
    /// </summary>
    public class AOITestSwitchImageCompleteHandler : ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AOITestSwitchImageCompleteHandler));

        public string EventName => "AOITestSwitchImageComplete";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            SocketControl.Current.Stream = stream;
            var relayManager = SocketRelayManager.GetInstance();

            log.Info($"收到外部Client返回: AOITestSwitchImageComplete, MsgID={request.MsgID}");

            // 构造响应转发给Flow
            var responseToFlow = new SocketResponse
            {
                Version = request.Version,
                MsgID = request.MsgID,
                EventName = "AOITestSwitchImageComplete",
                SerialNumber = request.SerialNumber,
                Code = 0,
                Msg = "SwitchImage completed"
            };

            relayManager.ForwardToFlow("1");

            log.Info("AOITestSwitchImageComplete 已转发给Flow");
            return null;
        }
    }
}
