using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Menus
{
    public class MenuItemMetadata : IMenuItem
    {
        public string? OwnerGuid { get; set; }

        public string? GuidId { get; set; }

        public int Order { get; set; } = 1;

        public string? Header { get; set; }

        public string? InputGestureText { get; set; }

        public object? Icon { get; set; }

        public ICommand? Command { get; set; }
        public Visibility Visibility { get; set; }
    }

}
