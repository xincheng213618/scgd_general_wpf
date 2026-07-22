using ColorVision.Properties;
using ColorVision.UI.Menus;

namespace ColorVision.Update
{
    public class MenuChangeLog : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override string Header => Resources.ChangeLog;
        public override int Order => 1;

        public override void Execute()
        {
            ChangelogPage.Open();
        }
    }
}
