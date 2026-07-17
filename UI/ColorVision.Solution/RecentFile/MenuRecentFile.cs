using ColorVision.Common.MVVM;
using ColorVision.Solution.Editor;
using ColorVision.UI.Menus;

namespace ColorVision.Solution.RecentFile
{

    public class MenuRecentFile : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.File;
        public override int Order => 80;
        public override string Header => Properties.Resources.RecentFiles;
    }


    public class MenuRecentFileProvider : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            List<MenuItemMetadata> menuItemMetas = new List<MenuItemMetadata>();
            List<string> recentFiles = SolutionManager.GetInstance().SolutionHistory.RecentFiles
                .Where(SolutionManager.IsSupportedOpenPath)
                .ToList();

            for (int i = 0; i < recentFiles.Count; i++)
            {
                string item = recentFiles[i];
                RelayCommand relayCommand = new RelayCommand(
                    _ => ResourceOpenService.Instance.TryOpen(item),
                    _ => SolutionManager.IsSupportedOpenPath(item));
                MenuItemMetadata menuItemMeta = new MenuItemMetadata
                {
                    OwnerGuid = nameof(MenuRecentFile),
                    GuidId = item.ToString(),
                    Header = item,
                    Icon = null, // Set your icon here if needed
                    Order = i,
                    Command = relayCommand,
                };
                menuItemMetas.Add(menuItemMeta);
            }
            return menuItemMetas;
        }
    }








    //public class MenuRecentFile : IMenuItemMeta
    //{
    //    public override string OwnerGuid => MenuItemConstants.File;

    //    public override string GuidId => "RecentFiles";

    //    public override int Order => 80;

    //    public override string Header => Resources.RecentFiles;

    //    public override MenuItem MenuItem
    //    {
    //        get
    //        {
    //            MenuItem RecentListMenuItem = null;

    //            RecentListMenuItem ??= new MenuItem();
    //            RecentListMenuItem.Header = Resources.RecentFiles;

    //            RecentListMenuItem.ItemsSource = SolutionManager.GetInstance().SolutionHistory.RecentFiles;
    //            RecentListMenuItem.Loaded += (s, e) =>
    //            {
    //                RecentListMenuItem.ItemsSource = SolutionManager.GetInstance().SolutionHistory.RecentFiles;
    //            };

    //            // Define the ItemTemplate
    //            DataTemplate dataTemplate = new DataTemplate(typeof(string));
    //            FrameworkElementFactory textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
    //            textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding());
    //            dataTemplate.VisualTree = textBlockFactory;

    //            // Set the ItemTemplate to the MenuItem
    //            RecentListMenuItem.ItemTemplate = dataTemplate;

    //            // Add event handler for item click
    //            RecentListMenuItem.ItemContainerStyle = new Style(typeof(MenuItem));
    //            RecentListMenuItem.ItemContainerStyle.Setters.Add(new EventSetter
    //            {
    //                Event = MenuItem.ClickEvent,
    //                Handler = new RoutedEventHandler(OpenSolutionCommandHandler)
    //            });
    //            return RecentListMenuItem;
    //        }
    //    }

    //    private void OpenSolutionCommandHandler(object sender, RoutedEventArgs e)
    //    {
    //        if (sender is MenuItem menuItem && menuItem.Header is string item)
    //        {
    //            SolutionManager.GetInstance().OpenSolution(item);
    //        }
    //    }
    //}
}
