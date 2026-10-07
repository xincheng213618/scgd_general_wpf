using ColorVision.SocketProtocol;
using log4net;
using System.Net.Sockets;

namespace ProjectARVRPro.Services;

public sealed class AOITestSwitchImageCompleteHandler : ISocketJsonHandler
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(AOITestSwitchImageCompleteHandler));
    public string EventName => ExternalImageSwitchService.CompletionEvent;

    public SocketResponse Handle(NetworkStream stream, SocketRequest request)
    {
        // A completion cannot claim the active connection or complete another client's operation.
        bool completed = ExternalImageSwitchService.Instance.TryComplete(stream, request);
        Log.Info($"AOI image switch acknowledgement: MsgID={request.MsgID}, Accepted={completed}");
        return null!;
    }
}
