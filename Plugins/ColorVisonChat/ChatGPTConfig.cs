using ColorVision.UI;

namespace ColorVisonChat
{
    public class ChatGPTConfig:IConfig
    {
        public static ChatGPTConfig Instance => ConfigService.Instance.GetRequiredService<ChatGPTConfig>();
        public string APiKey { get; set; }
    }
}
