using ColorVision.UI;

namespace ColorVisonChat
{
    public class ChatGPTConfig:IConfig
    {
        public static ChatGPTConfig Instance =>ConfigHandler.GetInstance().GetRequiredService<ChatGPTConfig>();
        public string APiKey { get; set; }
    }
}
