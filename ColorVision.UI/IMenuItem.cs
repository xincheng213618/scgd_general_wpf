using ColorVision.Common.MVVM;
using System.Windows.Controls;

namespace ColorVision.UI
{
    public interface IMenuItem
    {
        public string? OwnerGuid { get; }
        public string? GuidId { get;}
        public int Order { get; }
        public string? Header { get; }
        public string? InputGestureText { get; }
        public object? Icon { get; }
        public RelayCommand Command { get; }
    }


    public interface IMenuItemMeta : IMenuItem
    {
        public MenuItem MenuItem { get; }
    }
}
