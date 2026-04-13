using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;

namespace ColorVision
{
    /// <summary>
    /// Copilot 配置类，用于存储 API Key 等设置
    /// </summary>
    public class CopilotConfig : ViewModelBase, IConfig
    {
        public static CopilotConfig Instance => ConfigHandler.GetInstance().GetRequiredService<CopilotConfig>();

        /// <summary>
        /// API Key（加密存储）
        /// </summary>
        [DisplayName("API Key")]
        [Description("Kimi API Key")]
        public string ApiKey { get => _ApiKey; set { _ApiKey = value; OnPropertyChanged(); } }
        private string _ApiKey = string.Empty;

        /// <summary>
        /// 当前模式（ASK / AGENT）
        /// </summary>
        [DisplayName("模式")]
        [Description("Copilot 工作模式")]
        public CopilotMode Mode { get => _Mode; set { _Mode = value; OnPropertyChanged(); } }
        private CopilotMode _Mode = CopilotMode.Ask;

        /// <summary>
        /// API 基础地址
        /// </summary>
        [DisplayName("API 基础地址")]
        public string BaseUrl { get => _BaseUrl; set { _BaseUrl = value; OnPropertyChanged(); } }
        private string _BaseUrl = "https://api.moonshot.cn/v1";

        /// <summary>
        /// 模型名称
        /// </summary>
        [DisplayName("模型")]
        public string Model { get => _Model; set { _Model = value; OnPropertyChanged(); } }
        private string _Model = "moonshot-v1-8k";
    }

    public enum CopilotMode
    {
        [Description("ASK")]
        Ask,
        [Description("AGENT")]
        Agent
    }
}
