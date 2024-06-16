using ColorVision.UI;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.MySql
{
    public class MySqlInitializer : IInitializer
    {
        private readonly IMessageUpdater _messageUpdater;

        public MySqlInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }

        public int Order => 1;

        public async Task InitializeAsync()
        {
            if (MySqlSetting.Instance.IsUseMySql)
            {
                _messageUpdater.UpdateMessage("正在检测MySql数据库连接情况");

                bool isConnect = await MySqlControl.GetInstance().Connect();

                _messageUpdater.UpdateMessage($"MySql数据库连接{(MySqlControl.GetInstance().IsConnect ? Engine.Properties.Resources.Success : Engine.Properties.Resources.Failure)}");
                if (!isConnect)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MySqlConnect mySqlConnect = new() { Owner = Application.Current.MainWindow };
                        mySqlConnect.ShowDialog();
                    });
                }
            }
            else
            {
                _messageUpdater.UpdateMessage("已经跳过数据库连接");
                await Task.Delay(10);
            }
        }
    }
}
