using ColorVision.Themes.Controls;
using System;
using ColorVision.Common.MVVM;
using System.Windows;
using ColorVision.Themes;
using ColorVision.Engine.UserSpace;
using ColorVision.UserSpace;

namespace ColorVision.Engine.UserSpace
{
    /// <summary>
    /// UserEdit.xaml 的交互逻辑
    /// </summary>
    public partial class UserEdit : BaseWindow
    {
        public UserManager UserManager { get; set; }
        public UserConfig UserConfigCopy { get; set; }

        public UserEdit(UserManager userManager)
        {
            UserManager = userManager;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            UserConfigCopy = UserManager.Config.Clone();
        }


        private void Cancel_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
