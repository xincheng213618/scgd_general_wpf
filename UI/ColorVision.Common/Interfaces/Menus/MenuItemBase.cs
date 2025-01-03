using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Menus
{

    public abstract class MenuItemBase : ViewModelBase,IMenuItem
    {
        public abstract string OwnerGuid { get; }
        public abstract string GuidId { get; }
        public virtual int Order => 1;

        public abstract string Header { get; }
        public virtual Visibility Visibility => Visibility.Visible;
        public virtual string? InputGestureText { get; }
        public virtual object? Icon { get; }
        public virtual ICommand? Command => RelayCommand;   
        public virtual RelayCommand RelayCommand => new(A => Execute(), b => AccessControl.Check(Execute));
        public virtual void Execute()
        {
        }
    }

}
