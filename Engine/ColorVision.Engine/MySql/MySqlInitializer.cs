using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.MySql
{

    public class MySqlInitializer : InitializerBase
    {
        private readonly IMessageUpdater _messageUpdater;

        public MySqlInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }
        public override string Name => nameof(MySqlInitializer);
        public override int Order => 1;

        public override  async Task InitializeAsync()
        {
            if (MySqlSetting.Instance.IsUseMySql)
            {
                _messageUpdater.Update("正在检测MySql数据库连接情况");
                bool isConnect = await MySqlControl.GetInstance().Connect();

                _messageUpdater.Update($"MySql数据库连接{(MySqlControl.GetInstance().IsConnect ? Properties.Resources.Success : Properties.Resources.Failure)}");

                if (!isConnect)
                {
                    if (MySqlControl.Config.Host == "127.0.0.1" || MySqlControl.Config.Host == "localhost")
                    {
                        try
                        {
                            ServiceController serviceController = new ServiceController("MySQL");
                            try
                            {
                                var status = serviceController.Status;
                                _messageUpdater.Update($"检测服务，状态{status}，正在尝试启动服务");
                                if (Tool.IsAdministrator())
                                {
                                    serviceController.Start();
                                    isConnect = await MySqlControl.GetInstance().Connect();
                                    if (isConnect) return;
                                }
                                else
                                {
                                    if (Tool.ExecuteCommandAsAdmin("net start MySQL"))
                                    {
                                        isConnect = await MySqlControl.GetInstance().Connect();
                                        if (isConnect) return;
                                    }
                                }
                            }
                            catch (InvalidOperationException)
                            {
                                // 服务不存在
                                if (File.Exists(MySqlLocalConfig.Instance.MysqldPath))
                                {
                                    _messageUpdater.Update("MySQL服务未安装，正在尝试手动安装MySQL服务。");

                                    string cmd = $"{MySqlLocalConfig.Instance.MysqldPath} --install MySQL&&net start MySQL";
                                    if (Tool.ExecuteCommandAsAdmin(cmd))
                                    {
                                        isConnect = await MySqlControl.GetInstance().Connect();
                                        if (isConnect) return;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _messageUpdater.Update(ex.Message);
                        }
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MySqlConnect mySqlConnect = new() { Owner = Application.Current.MainWindow };
                        mySqlConnect.ShowDialog();
                    });
                }
                
            }
        }
    }
}
