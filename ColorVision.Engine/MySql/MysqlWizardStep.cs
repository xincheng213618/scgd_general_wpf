using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.MySql
{
    public class MysqlWizardStep : IWizardStep
    {
        public int Order => 1;

        public string Header => "Mysql配置";

        public RelayCommand Command => new RelayCommand(a =>
        {
            MySqlConnect mySqlConnect = new() { Owner = Application.Current.GetActiveWindow()};
            mySqlConnect.ShowDialog();
        });
    }
}
