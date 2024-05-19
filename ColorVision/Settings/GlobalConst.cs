using System.Collections.Generic;

namespace ColorVision.Settings
{
    public static partial class GlobalConst
    {
        public const string ConfigFileName = "Config\\SoftwareConfig.json";

        public const string ConfigDIFileName = "Config\\ColorVisionConfig.json";

        public const string MQTTMsgRecordsFileName = "Config\\MsgRecords.json";

        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "ColorVision";

        public const string ConfigPath = "Config";

        public const string AutoRunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AutoRunName = "ColorVisionAutoRun";

        public static readonly List<string> LogLevel = new() { "all","debug", "info", "warning", "error", "none" };
    }
}
