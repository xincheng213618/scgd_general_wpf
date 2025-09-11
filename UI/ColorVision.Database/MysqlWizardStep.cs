using ColorVision.UI;
using System.Windows;

namespace ColorVision.Database
{
    public class MysqlWizardStep : WizardStepBase
    {
        public override int Order => 9;

        public override string Header => "Mysql配置";

        public override string Description => "用户可以在这里配置数据库的连接";

        public override void Execute()
        {
            MySqlConnect mySqlConnect = new() { Owner = Application.Current.GetActiveWindow() };
            mySqlConnect.ShowDialog();
        }

    }
}
