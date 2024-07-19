using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Properties;
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


    public class MenuExit : MenuItemBase
    {
        public override string OwnerGuid => "File";
        public override string GuidId => "MenuExit";
        public override string Header => Resources.MenuExit;
        public override string? InputGestureText => "Alt + F4";
        public override int Order => 1000000;

        public override void Execute()
        {
            Environment.Exit(0);
        }
    }

}
