using ColorVision.Properties;
using ColorVision.UI.Authorizations;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorVision.Update
{
    public class MenuForceUpdate : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override string Header => Resources.ForceUpdate;
        public override int Order => 100;

        public override void Execute()
        {
            Task.Run(() => AutoUpdater.GetInstance().ForceUpdate());
        }
    }
    public class MemuCheckAndUpdate : MenuItemBase, IHotKey
    {
        public HotKeys HotKeys => new HotKeys(Properties.Resources.Update, new Hotkey(Key.U, ModifierKeys.Control), Execute);

        public override string OwnerGuid => nameof(MenuUpdate);
        public override int Order => 100;
        public override string Header => "检查主体和插件更新";
        public override string InputGestureText => "Ctrl + U";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute() => _ = CombinedUpdateCoordinator.StartInteractiveAsync();
    }
}
