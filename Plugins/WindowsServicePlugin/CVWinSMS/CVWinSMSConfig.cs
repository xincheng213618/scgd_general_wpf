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

        public string UpdatePath { get => _UpdatePath; set { _UpdatePath = value; OnPropertyChanged(); } }
        private string _UpdatePath = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool";
    }
}
