using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Menus
{
    public abstract class IMenuItemMeta : IMenuItem
    {
        public virtual string OwnerGuid => MenuItemConstants.Menu;
        public virtual string GuidId => GetType().Name;

        public virtual int Order => 1;

        public virtual string Header { get; }

        public virtual MenuItem MenuItem { get; }
        public virtual Visibility Visibility => Visibility.Visible;
        public virtual string? InputGestureText { get; }
        public virtual object? Icon { get; }
        public virtual ICommand? Command => null;
    }

}
