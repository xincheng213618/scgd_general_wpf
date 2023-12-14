using ColorVision.Themes.Controls;
using System;
using ColorVision.MVVM;
using System.Windows;

namespace ColorVision.User
{
    /// <summary>
    /// UserEdit.xaml 的交互逻辑
    /// </summary>
    public partial class UserEdit : BaseWindow
    {
        public UserConfig UserConfig { get; set; }
        public UserConfig UserConfigCopy { get; set; }

        public UserEdit(UserConfig userConfig)
        {
            UserConfig = userConfig;
            UserConfigCopy = new UserConfig();
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            UserConfigCopy.CopyTo(UserConfig);
        }
    }
}
