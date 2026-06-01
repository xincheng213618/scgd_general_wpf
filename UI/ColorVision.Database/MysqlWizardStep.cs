using ColorVision.UI;
using System.Windows;
using ColorVision.Database.Properties;

namespace ColorVision.Database
{
    public class MysqlWizardStep : WizardStepBase
    {
        public override int Order => 9;

        public override string Header => Properties.Resources.DB_MysqlConfig;

        public override string Description => Properties.Resources.DB_MysqlConfigDesc;

        public override void Execute()
        {
            MySqlConnect mySqlConnect = new() { Owner = Application.Current.GetActiveWindow() };
            mySqlConnect.ShowDialog();
        }

    }
}
