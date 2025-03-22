#pragma warning disable SYSLIB0014
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.IO;
using System.Windows;
using System.Xml.Linq;

namespace WindowsServicePlugin
{
    public partial class SetServiceConfig
    {
        public class SetMysqlConfig : WizardStepBase
        {
            public override int Order => 8;

            public override string Header => "从服务中配置Mysql";
            public override string Description => "如果已经正确配置服务管理工具，使用该命令会自动读取服务管理工具中的配置文件并应用";

            Dictionary<string, string> dic = new Dictionary<string, string>();
            public override void Execute()
            {
                if (!File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
                {
                    MessageBox.Show("请先配置服务管理工具");
                    return;
                }
                string filePath = Directory.GetParent(CVWinSMSConfig.Instance.CVWinSMSPath) + @"\config\App.config";

                if (!File.Exists(filePath))
                {
                    MessageBox.Show("请先运行服务管理工具");
                    return;
                }
                // Load the XML document
                XDocument config = XDocument.Load(filePath);

                // Query the appSettings
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
                    MySqlSetting.Instance.MySqlConfig.Host = dic["MysqlHost"];

                    MySqlSetting.Instance.MySqlConfig.UserName = dic["MysqlUser"];
                    MySqlSetting.Instance.MySqlConfig.UserPwd = dic["MysqlPwd"];
                    MySqlSetting.Instance.MySqlConfig.Database = dic["MysqlDatabase"];
                    MySqlConfig rootConfig = new MySqlConfig() { Name = "RootPath", Host = dic["MysqlHost"], UserName = "root", UserPwd = dic["MysqlRootPwd"], Database = dic["MysqlDatabase"] };
                    var oldrootConfig = MySqlSetting.Instance.MySqlConfigs.First(a => a.Name == "RootPath");
                    MySqlSetting.Instance.MySqlConfigs.Remove(oldrootConfig);
                    MySqlSetting.Instance.MySqlConfigs.Add(rootConfig);

                    CVWinSMSConfig.Instance.Version = dic["Version"];
                    MessageBox.Show("配置成功");
                }
                else
                {
                }
            }
        }
    }
}
    



