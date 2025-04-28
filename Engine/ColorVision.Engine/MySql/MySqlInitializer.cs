using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
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
                    if (MySqlControl.Config.Host == "127.0.0.1")
                    {
                        try
                        {

                            ServiceController ServiceController = new ServiceController("MySQL");
                            if (ServiceController != null)
                            {
                                _messageUpdater.Update($"检测服务，状态{ServiceController.Status}，正在尝试启动服务");
                                if (Tool.IsAdministrator())
                                {
                                    ServiceController.Start();
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
