using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services.RC;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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
