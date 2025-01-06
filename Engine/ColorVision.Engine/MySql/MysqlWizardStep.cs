using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.MySql
{
    public class MysqlWizardStep : IWizardStep
    {
        public int Order => 9;

        public string Header => "Mysql配置";
        public string Description => "用户可以在这里配置数据库的连接，默认用户是root";

        public RelayCommand RelayCommand => new RelayCommand(a =>
        {
            MySqlConnect mySqlConnect = new() { Owner = Application.Current.GetActiveWindow()};
            mySqlConnect.ShowDialog();
        });
    }
}
