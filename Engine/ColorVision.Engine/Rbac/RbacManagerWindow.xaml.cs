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
    public class MenuRbacManager : IRightMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
            menuItemMetadata.Command = new RelayCommand(a => new RbacManagerWindow() {  Owner =Application.Current.GetActiveWindow(),WindowStartupLocation =WindowStartupLocation.CenterOwner}.ShowDialog() );
            menuItemMetadata.Icon = new Image()
            {
                Source = (ImageSource)Application.Current.Resources["DrawingImageUser"],
            };
            return new MenuItemMetadata[] { menuItemMetadata };
        }
    }

    /// <summary>
    /// RbacManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class RbacManagerWindow : Window
    {
        public RbacManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = RbacManager.GetInstance();
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
