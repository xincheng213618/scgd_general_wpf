#pragma warning disable SYSLIB0014
using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.IO;
using System.Xml.Linq;


namespace WindowsServicePlugin.CVWinSMS
{
    public class CVWinSMSConfig : ViewModelBase, IConfig,IConfigSettingProvider   
    {
        public static CVWinSMSConfig Instance => ConfigService.Instance.GetRequiredService<CVWinSMSConfig>();

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
            catch(Exception ex)
            {
                return;
            }
        }


        public string UpdatePath { get => _UpdatePath; set { _UpdatePath = value; NotifyPropertyChanged(); } }
        private string _UpdatePath = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool";

        public bool IsAutoUpdate { get => _IsAutoUpdate; set { _IsAutoUpdate = value; NotifyPropertyChanged(); } }
        private bool _IsAutoUpdate = true;

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>()
            {
                new ConfigSettingMetadata()
                {
                    Name = "CVWinSMSPath",
                    Description = "CVWinSMSPath",
                    Type = ConfigSettingType.Text,
                    BindingName = nameof(CVWinSMSPath),
                    Source =Instance
                },
                new ConfigSettingMetadata
                {
                    Name = "CVWinSMSIsAutoUpdate",
                    Description =  "",
                    Order = 999,
                    Type = ConfigSettingType.Bool,
                    BindingName =nameof(IsAutoUpdate),
                    Source = Instance,
                }
            };

        }
    }
}
