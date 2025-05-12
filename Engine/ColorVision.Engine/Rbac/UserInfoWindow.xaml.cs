using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Rbac
{
    public class MenuUserInfo : IRightMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
            menuItemMetadata.Command = new RelayCommand(a => new UserInfoWindow().ShowDialog());
            menuItemMetadata.Icon = new Image()
            {
                Source = (ImageSource)Application.Current.Resources["DrawingImageUser"],
            };
            return new MenuItemMetadata[] { menuItemMetadata };
        }
    }

    /// <summary>
    /// UserInfoWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UserInfoWindow : Window
    {
        public UserInfoWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = UserManager.GetInstance();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
