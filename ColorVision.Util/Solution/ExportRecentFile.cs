using ColorVision.Common.MVVM;
using ColorVision.RecentFile;
using ColorVision.UI.Menus;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Util.Properties;

namespace ColorVision.Solution
{
    public class ExportRecentFile : IMenuItemMeta
    {
        public string? OwnerGuid => "File";

        public string? GuidId => "RecentFiles";

        public int Order => 2;

        public string? Header => Resources.RecentFiles;

        public string? InputGestureText => null;

        public object? Icon => null;
        public RelayCommand Command => new(a => { });

        private RecentFileList SolutionHistory = new() { Persister = new RegistryPersister("Software\\ColorVision\\SolutionHistory") };
        public Visibility Visibility => Visibility.Visible;

        public MenuItem MenuItem
        {
            get
            {
                MenuItem RecentListMenuItem = null;

                RecentListMenuItem ??= new MenuItem();
                RecentListMenuItem.Header = Resources.RecentFiles;
                RecentListMenuItem.SubmenuOpened += (s, e) =>
                {
                    var firstMenuItem = RecentListMenuItem.Items[0];
                    foreach (var item in SolutionHistory.RecentFiles)
                    {
                        if (File.Exists(item))
                        {
                            MenuItem menuItem = new();
                            menuItem.Header = item;
                            menuItem.Click += (sender, e) =>
                            {
                                SolutionManager.GetInstance().OpenSolution(item);
                            };
                            RecentListMenuItem.Items.Add(menuItem);
                        }
                        else
                        {
                            SolutionHistory.RecentFiles.Remove(item);
                        }
                    };
                    RecentListMenuItem.Items.Remove(firstMenuItem);

                };
                RecentListMenuItem.SubmenuClosed += (s, e) =>
                {
                    RecentListMenuItem.Items.Clear();
                    RecentListMenuItem.Items.Add(new MenuItem());
                };
                RecentListMenuItem.Items.Add(new MenuItem());
                return RecentListMenuItem;
            }
        }
    }
}
