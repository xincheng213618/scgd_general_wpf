#pragma warning disable CS8604
using ColorVision.UI;
using System.ComponentModel;
using System.Net.Sockets;
using System.Reflection;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// 文本消息分发器
    /// 自动扫描并注册所有实现ISocketTextDispatcher接口的处理器
    /// </summary>
    public class SocketTextDispatcher
    {
        private readonly List<ISocketTextDispatcher> _handlers = new List<ISocketTextDispatcher>();

        public SocketTextDispatcher()
        {
            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(ISocketTextDispatcher).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    {
                        var displayNameAttr = type.GetCustomAttribute<DisplayNameAttribute>();
                        var eventName = displayNameAttr?.DisplayName ?? type.Name;
                        if (Activator.CreateInstance(type) is ISocketTextDispatcher handler)
                        {
                            _handlers.Add(handler);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 分发文本请求到对应的处理器
        /// </summary>
        /// <param name="stream">网络流</param>
        /// <param name="request">请求文本</param>
        /// <returns>响应文本</returns>
        public string? Dispatch(NetworkStream stream, string request)
        {
            if(_handlers.Count > 0)
            {
                foreach (var handle in _handlers)
                {
                    string respose = handle.Handle(stream, request);
                    if (!string.IsNullOrWhiteSpace(respose))
                        return respose;
                    else
                        return null;
                }
            }
            return "No Dispatcher Hanle";
        }
    }
}
