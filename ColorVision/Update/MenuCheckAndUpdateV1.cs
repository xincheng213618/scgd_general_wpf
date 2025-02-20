using ColorVision.UI.HotKey;
using System.Windows;
using System.Windows.Input;
using ColorVision.UI.Menus;
using ColorVision.UI.Authorizations;

namespace ColorVision.Update
{

    public class ExportIncrementUpdate : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);

        public override int Order => 10004;
        public override Visibility Visibility => Visibility.Visible;

        public override string Header => "增量更新";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute() => _ = AutoUpdater.GetInstance().CheckAndUpdate(true,true);
    }

    public class MemuCheckAndUpdate : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override int Order => 10003;
        public override string Header => "检查更新";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute() => _ = AutoUpdater.GetInstance().CheckAndUpdate();
    }

    public class MenuCheckAndUpdateV1: MenuItemBase, IHotKey
    {
        public HotKeys HotKeys => new HotKeys(Properties.Resources.Update, new Hotkey(Key.U, ModifierKeys.Control), Execute);

        public override string OwnerGuid => MenuItemConstants.Help;

        public override int Order => 10003;
        public override Visibility Visibility => Visibility.Visible;

        public override string Header => Properties.Resources.MenuUpdate;

        public override string InputGestureText => "Ctrl + U";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute() => _ = AutoUpdater.GetInstance().CheckAndUpdateV1();
    }
}
