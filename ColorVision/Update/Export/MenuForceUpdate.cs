using ColorVision.Properties;
using ColorVision.UI.Menus;
using System.Threading.Tasks;

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
    public class MemuCheckAndUpdate : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override int Order => 100;
        public override string Header => ColorVision.Properties.Resources.CheckUpdateOld;

        public override void Execute() => _ = AutoUpdater.GetInstance().CheckAndUpdate();
    }
}
