using ColorVision.Common.MVVM;
using ColorVision.UI.Configs;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Menus
{
    public interface IMenuItem
    {
        public string? OwnerGuid { get; }
        public string? GuidId { get; }
        public int Order { get; }
        public string? Header { get; }
        public string? InputGestureText { get; }
        public object? Icon { get; }
        public RelayCommand? Command { get; }

        public Visibility Visibility { get; }
    }

    public class MenuItemMetadata : IMenuItem
    {
        public string? OwnerGuid { get; set; }

        public string? GuidId { get; set; }

        public int Order { get; set; } = 1;

        public string? Header { get; set; }

        public string? InputGestureText { get; set; }

        public object? Icon { get; set; }

        public RelayCommand? Command { get; set; }
        public Visibility Visibility { get; set; }
    }

    public interface IMenuItemProvider
    {
        IEnumerable<MenuItemMetadata> GetMenuItems();
    }


    public interface IMenuItemMeta : IMenuItem
    {
        public MenuItem MenuItem { get; }
    }

}
