using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using System.Windows;

namespace ColorVision.UI.Menus
{

    public abstract class MenuItemBase : ViewModelBase,IMenuItem
    {
        public abstract string OwnerGuid { get; }
        public abstract string GuidId { get; }
        public abstract string Header { get; }

        public virtual int Order => 1;
        public virtual Visibility Visibility => Visibility.Visible;
        public virtual string? InputGestureText { get; }
        public virtual object? Icon { get; }
        public virtual RelayCommand Command => new(A => Execute(), b => AccessControl.Check(Execute));
        public virtual void Execute()
        {
        }
    }

}
