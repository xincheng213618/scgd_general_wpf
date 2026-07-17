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
            List<MenuItemMetadata> menuItemMetas = new();
            foreach (string item in SolutionManager.GetInstance().SolutionHistory.RecentFiles)
            {
                if (!ResourceOpenService.TryDescribeWorkspaceResource(
                    item,
                    out WorkspaceResourceInfo resourceInfo))
                {
                    continue;
                }

                int index = menuItemMetas.Count + 1;
                RelayCommand relayCommand = new RelayCommand(
                    async _ => await ResourceOpenService.Instance.TryOpenWithFeedbackAsync(item),
                    _ => SolutionManager.IsSupportedOpenPath(item));
                MenuItemMetadata menuItemMeta = new MenuItemMetadata
                {
                    OwnerGuid = nameof(MenuRecentFile),
                    GuidId = resourceInfo.FullPath,
                    Header = $"_{index}  {resourceInfo.DisplayName}  [{resourceInfo.KindDisplayName}] — {resourceInfo.FullPath}",
                    Icon = null, // Set your icon here if needed
                    Order = index - 1,
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
