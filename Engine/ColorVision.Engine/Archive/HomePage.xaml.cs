using ColorVision.Engine.Archive.Dao;
using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.UI.Pages;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace ColorVision.Engine.Archive
{
    public class MenuArchiveManager : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuArchive);
        public override string Header => ColorVision.Engine.Properties.Resources.ArchiveQuery;

        public override void Execute()
        {

            Frame frame = new Frame();
            HomePage homePage = new HomePage(frame);
            frame.Navigate(homePage);

            Window window = new Window();
            window.Content = frame;
            window.Show();
        }
    }

    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    [Page(nameof(HomePage))]
    public partial class HomePage : Page, IPage
    {
        public Frame Frame { get; set; }

        public HomePage() { }

        public HomePage(Frame frame)
        {
            Frame = frame;
            InitializeComponent();
        }
        private void Page_Initialized(object sender, EventArgs e) 
        {
            //ContentStackPanel.AddAdorners(this);

        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void UserControl_PreviewKeyUp(object sender, KeyEventArgs e)
        {
        }

        private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UserControl userControl)
            {
                Frame.Navigate(PageManager.Instance.GetPage(userControl.Tag.ToString(), Frame));
            }
        }
    }
}
