using ColorVision.UI.Properties;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ColorVision.UI.Menus.Base
{

    public class MenuEdit : MenuItemMenuBase
    {
        public override string GuidId => MenuItemConstants.Edit;
        public override string Header => Resources.MenuEdit;
        public override int Order => 2;
    }
    public abstract class MenuItemEditBase : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Edit;
    }
}
