using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.IO;
using System.Xml.Linq;


namespace WindowsServicePlugin.CVWinSMS
{
    public class CVWinSMSConfig : ViewModelBase, IConfig
    {
        public static CVWinSMSConfig Instance => ConfigService.Instance.GetRequiredService<CVWinSMSConfig>();

        [DisplayName("CVWinSMSPath")]
        [Description("CVWinSMSPathDescription")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string CVWinSMSPath { get => _CVWinSMSPath; set  { _CVWinSMSPath = value; } }
        private string _CVWinSMSPath = string.Empty;

        [JsonIgnore]
        public string BaseLocation { 
            
            get
            {
                if (dic.TryGetValue("BaseLocation", out string location))
                    return location;
                return string.Empty;
            }
        }

        [JsonIgnore]
        Dictionary<string, string> dic = new Dictionary<string, string>();

        public void Init()
        {
            try
            {
                string filePath = Directory.GetParent(CVWinSMSPath) + @"\config\App.config";
                if (!File.Exists(filePath))
                {
                    return;
                }
                XDocument config = XDocument.Load(filePath);
                var appSettings = config.Element("configuration")?.Element("appSettings")?.Elements("add");

                if (appSettings != null)
                {
                    foreach (var setting in appSettings)
                    {
                        string key = setting.Attribute("key")?.Value;
                        string value = setting.Attribute("value")?.Value;
                        if (key != null && value != null)
                        {
                            if (!dic.TryAdd(key, value))
                            {
                                dic[key] = value;
                            }
                        }
                    }
                }
            }
            catch
            {
                return;
            }
        }

    }
}
