using ColorVision.Common.MVVM;
using ColorVision.UI.Configs;
using System.Windows;

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

}
