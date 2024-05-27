using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.MySql
{
    public class MysqlWizardStep : IWizardStep
    {
        public int Order => 1;

        public string Title => "Mysql配置";

        public string Description => "Mysql配置";

        public RelayCommand? RelayCommand => new RelayCommand(a =>
        {
            MySqlConnect mySqlConnect = new() { Owner = Application.Current.GetActiveWindow()};
            mySqlConnect.ShowDialog();
        });
    }
}
