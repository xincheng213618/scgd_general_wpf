using ColorVision.Solution.Properties;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Solution
{
    public class MenuRecentFile : IMenuItemMeta
    {
        public string? OwnerGuid => MenuItemConstants.File;

        public string? GuidId => "RecentFiles";

        public int Order => 80;

        public string? Header => Resources.RecentFiles;

        public string? InputGestureText => null;

        public object? Icon => null;
        public ICommand Command => null;

        public Visibility Visibility => Visibility.Visible;

        public MenuItem MenuItem
        {
            get
            {
                MenuItem RecentListMenuItem = null;

                RecentListMenuItem ??= new MenuItem();
                RecentListMenuItem.Header = Resources.RecentFiles;

                RecentListMenuItem.ItemsSource = SolutionManager.GetInstance().SolutionHistory.RecentFiles;
                RecentListMenuItem.Loaded += (s, e) =>
                {
                    RecentListMenuItem.ItemsSource = SolutionManager.GetInstance().SolutionHistory.RecentFiles;
                };

                // Define the ItemTemplate
                DataTemplate dataTemplate = new DataTemplate(typeof(string));
                FrameworkElementFactory textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
                textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding());
                dataTemplate.VisualTree = textBlockFactory;

                // Set the ItemTemplate to the MenuItem
                RecentListMenuItem.ItemTemplate = dataTemplate;

                // Add event handler for item click
                RecentListMenuItem.ItemContainerStyle = new Style(typeof(MenuItem));
                RecentListMenuItem.ItemContainerStyle.Setters.Add(new EventSetter
                {
                    Event = MenuItem.ClickEvent,
                    Handler = new RoutedEventHandler(OpenSolutionCommandHandler)
                });
                return RecentListMenuItem;
            }
        }

        private void OpenSolutionCommandHandler(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Header is string item)
            {
                SolutionManager.GetInstance().OpenSolution(item);

            }
        }
    }
}
