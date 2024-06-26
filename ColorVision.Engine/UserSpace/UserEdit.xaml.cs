using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.UserSpace
{
    /// <summary>
    /// UserEdit.xaml 的交互逻辑
    /// </summary>
    public partial class UserEdit : Window
    {
        public UserManager UserManager { get; set; }
        public UserDetailModel UserConfigCopy { get; set; }

        public UserEdit(UserManager userManager)
        {
            UserManager = userManager;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.Title = $"{Properties.Resources.Edit} {UserManager.Config.Account} 个人资料"; 
            UserConfigCopy = UserManager.UserDetailModel.Clone();
            this.DataContext = UserManager;
            EditContent.DataContext = UserConfigCopy;

            CmPerMissionMode.ItemsSource = from e1 in Enum.GetValues(typeof(PermissionMode)).Cast<PermissionMode>()
                                           select new KeyValuePair<PermissionMode, string>(e1, e1.ToString());
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            UserManager.UserDetailModel.CopyFrom(UserConfigCopy);
            this.Close();

        }
    }
}
