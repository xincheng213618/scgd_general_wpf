using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ColorVision.UI.Menus
{
    public static class MenuItemIcon
    {
        public static object? TryFindResource(object resourceKey)
        {
            if (Application.Current.TryFindResource(resourceKey) is Brush brush)
            {
                var rectangle = new Rectangle() { Height = 16, Width = 16 };
                rectangle.SetResourceReference(Rectangle.FillProperty, resourceKey);
                return new Viewbox() { Height = 16, Width = 16, Child = rectangle };

            }
            return null;
        }

        public static MenuItem ToMenuItem(this IMenuItem item)
        {
            MenuItem menuItem = new()
            {
                Header = item.Header,
                Icon = item.Icon,
                InputGestureText = item.InputGestureText,
                Command = item.Command,
                Tag = item,
                Visibility = item.Visibility,
            };
            return menuItem;
        }
    }

    public static class MenuItemConstants
    {
        public const string Menu = "Menu";
        public const string File = "File";
        public const string Edit = "Edit";
        public const string View = "View";
        public const string Tool = "Tool";
        public const string Help = "Help";
    }


    public interface IMenuItem
    {
        public string? OwnerGuid { get; }
        public string? GuidId { get; }
        public int Order { get; }
        public string? Header { get; }
        public string? InputGestureText { get; }
        public object? Icon { get; }

        public ICommand? Command { get; }

        public Visibility Visibility { get; }
        public bool? IsChecked { get; } // 新增的属性
    }

}
