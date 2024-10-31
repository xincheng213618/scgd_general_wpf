using ColorVision.Properties;
using ColorVision.UI.Menus;
using System.Threading.Tasks;

namespace ColorVision.Update.Export
{
    public class MenuForceUpdate : MenuItemBase
    {
        public override string OwnerGuid => "Update";
        public override string GuidId => "ForceUpdate";
        public override string Header => Resources.ForceUpdate;
        public override int Order => 100;

        public override void Execute()
        {
            Task.Run(() => AutoUpdater.GetInstance().ForceUpdate());
        }
    }
}
