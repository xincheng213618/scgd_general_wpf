using ColorVision.Solution.Searches;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace ColorVision.Engine.DataHistory
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class HomePage : Page, ISolutionPage
    {
        public string PageTitle => nameof(HomePage);

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
                Frame.Navigate(SolutionPageManager.Instance.GetPage(userControl.Tag.ToString(), Frame));
            }
        }
    }
}
