using ColorVision.UI.Views;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Xceed.Wpf.AvalonDock.Layout;

namespace ColorVision.Solution.Searches
{
    /// <summary>
    /// SolutionView.xaml 的交互逻辑
    /// </summary>
    public partial class SolutionView : UserControl, IView
    {
        public SolutionView()
        {
            InitializeComponent();
        }
        public View View { get; set; }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            View = new View();
            View.ViewIndexChangedEvent += View_ViewIndexChangedEvent;

            MainFrame.Navigate(new HomePage(MainFrame));

            if (Application.Current.FindResource("MenuItem4FrameStyle") is Style style)
            {
                ContextMenu content1 = new() { ItemContainerStyle = style };
                content1.SetBinding(ItemsControl.ItemsSourceProperty, new Binding { Path = new PropertyPath("BackStack"), Source = MainFrame });
                BackStack.ContextMenu = content1;

                ContextMenu content2 = new() { ItemContainerStyle = style };
                content2.SetBinding(ItemsControl.ItemsSourceProperty, new Binding { Path = new PropertyPath("ForwardStack"), Source = MainFrame });
                BrowseForward.ContextMenu = content2;
            }
            ContextMenu contextMenu = new();
            MainSetting.ContextMenu = contextMenu;
            MenuItem menuItem = new() { Header = "独立窗口" };
            menuItem.Click += (s, e) =>
            {
                View.ViewIndex = -2;
            };
            contextMenu.Items.Add(menuItem);;

            SolutionManager.GetInstance().OpenFile += (s, e) =>
            {
                LayoutDocument layoutDocument = new LayoutDocument() { ContentId = Guid.NewGuid().ToString(), Content = e, Title = e.ToolTip.ToString() };
                LayoutDocumentPane.Children.Add(layoutDocument);
                LayoutDocumentPane.SelectedContentIndex = LayoutDocumentPane.IndexOf(layoutDocument) ;
            };
        }

        private void View_ViewIndexChangedEvent(int oindex, int index)
        {
            if (index == -2)
            {
                ContextMenu contextMenu = new();
                MenuItem menuItem = new() { Header = "还原" };
                menuItem.Click += (s, e) =>
                {
                    View.ViewIndex = 0;
                };
                contextMenu.Items.Add(menuItem);
                MainSetting.ContextMenu = contextMenu;
                View.ViewGridManager?.SetViewIndex(this,-2);
            }
            if (index == 0)
            {
                ContextMenu contextMenu = new();
                MenuItem menuItem = new() { Header = "独立窗口" };
                menuItem.Click += (s, e) =>
                {
                    View.ViewIndex = -2;
                };
                contextMenu.Items.Add(menuItem);
                MainSetting.ContextMenu = contextMenu;
                View.ViewGridManager?.SetViewIndex(this, 0);

            }
        }
    }
}
