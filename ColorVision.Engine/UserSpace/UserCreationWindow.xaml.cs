using ColorVision.Common.Extension;
using ColorVision.Engine.Services;
using ColorVision.Themes;
using ColorVision.UI.Authorization;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.UserSpace
{
    /// <summary>
    /// UserCreationWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UserCreationWindow : Window
    {
        public UserCreationWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = UserConfig.Instance;
            CmPerMissionMode.ItemsSource = from e1 in Enum.GetValues(typeof(PermissionMode)).Cast<PermissionMode>()
                                           select new KeyValuePair<PermissionMode, string>(e1, e1.ToString());
        }
        private void Button_Click(object sender, EventArgs e)
        {
            Authorization.Instance.PermissionMode = UserConfig.Instance.PerMissionMode;
        }
    }
}
