using ColorVision.Common.MVVM;
using System.Windows;

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

        public RelayCommand? Command { get; set; }
        public Visibility Visibility { get; set; }
    }

}
