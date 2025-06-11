using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Properties;
using ColorVision.Themes;
using ColorVision.UI.Menus;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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

            for (int i = 0; i < SolutionManager.GetInstance().SolutionHistory.RecentFiles.Count; i++)
            {
                string item = SolutionManager.GetInstance().SolutionHistory.RecentFiles[i];
                RelayCommand relayCommand = new RelayCommand(a =>
                {
                    SolutionManager.GetInstance().OpenSolution(item);
                });
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
