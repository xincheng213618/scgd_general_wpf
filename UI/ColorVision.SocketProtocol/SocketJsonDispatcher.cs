#pragma warning disable CS8604
using ColorVision.UI;
using System.Net.Sockets;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// JSON消息分发器
    /// 自动扫描并注册所有实现ISocketJsonHandler接口的处理器
    /// </summary>
    public class SocketJsonDispatcher
    {
        private readonly Dictionary<string, ISocketJsonHandler> _handlers = new();

        public SocketJsonDispatcher()
        {
            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(ISocketJsonHandler).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is ISocketJsonHandler handler)
                    {
                        if (!_handlers.ContainsKey(handler.EventName))
                            _handlers[handler.EventName] = handler;
                    }
                }
            }
        }

        /// <summary>
        /// 分发Socket请求到对应的处理器
        /// </summary>
        /// <param name="stream">网络流</param>
        /// <param name="request">请求消息</param>
        /// <returns>响应消息</returns>
        public SocketResponse Dispatch(NetworkStream stream, SocketRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.EventName))
                return new SocketResponse { Code = 400, Msg = "Invalid request" };

            if (_handlers.TryGetValue(request.EventName, out var handler))
                return handler.Handle(stream, request);

            return new SocketResponse { Code = 404, Msg = "Handler not found for event: " + request.EventName };
        }
    }
}
