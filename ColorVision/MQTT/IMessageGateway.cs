using System;
using System.Threading.Tasks;

namespace ColorVision.MQTT
{
    public interface IMessageGateway
    {
        // 发送消息的方法，可以根据需要添加更多参数或者创建专门的消息类
        Task SendMessageAsync(string destination, string message);

        // 发送消息并等待响应的方法，适用于需要请求/响应模式的场景
        Task<string> SendRequestAsync(string destination, string message);

        // 可能还需要一个方法来发布消息到特定的主题或频道
        Task PublishMessageAsync(string topic, string message);

        // 订阅特定主题或频道的方法
        Task SubscribeAsync(string topic, Action<string> onMessageReceived);

        // 取消订阅的方法
        Task UnsubscribeAsync(string topic);

        // 可以添加连接和断开连接的方法，这在某些通讯协议中是必要的
        Task ConnectAsync();
        Task DisconnectAsync();

       }

}
