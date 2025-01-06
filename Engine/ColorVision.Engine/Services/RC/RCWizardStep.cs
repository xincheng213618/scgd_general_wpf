using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.MySql
{
    public class RCWizardStep : IWizardStep
    {
        public int Order => 11;

        public string Header => "RC配置";
        public string Description => "配置注册中心，如果已经正确配置服务可以点击服务配置即可不需要手动配置";

        public RelayCommand RelayCommand => new RelayCommand(a =>
        {
            RCServiceConnect rCServiceConnect = new() { Owner = Application.Current.GetActiveWindow()};
            rCServiceConnect.ShowDialog();
        });
    }
}
