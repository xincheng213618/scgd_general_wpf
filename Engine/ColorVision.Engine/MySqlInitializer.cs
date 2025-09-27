using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using ColorVision.UI.CUDA;
using log4net;
using System;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine
{
    public class MySqlInitializer : InitializerBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlInitializer));
        public override string Name => nameof(MySqlInitializer);
        public override int Order => 1;

        public override  async Task InitializeAsync()
        {
            if (MySqlSetting.Instance.IsUseMySql)
            {
                log.Info("正在检测MySql数据库连接情况");
                bool isConnect = await MySqlControl.GetInstance().Connect();

                log.Info($"MySql数据库连接{(MySqlControl.GetInstance().IsConnect ? Properties.Resources.Success : Properties.Resources.Failure)}");

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
                                log.Info($"检测服务，状态{status}，正在尝试启动服务");
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
                                    log.Info("MySQL服务未安装，正在尝试手动安装MySQL服务。");

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
                            log.Error(ex.Message);
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
