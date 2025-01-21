using ColorVision.UI.Properties;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Menus.Base
{
    public class MenuFile : MenuItemMenuBase
    {
        public override string GuidId => MenuItemConstants.File;
        public override string Header => Resources.MenuFile;
        public override int Order => 1;
    }

    public abstract class MenuItemFileBase : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.File;
    }

}
