using ColorVision.UI.Menus;

namespace ColorVision.Engine
{
    public class MenuOpendeepHelp : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override string Header => "查询帮助";
        public override int Order => -1;
        public override void Execute()
        {
            Common.Utilities.PlatformHelper.Open("https://opendeep.wiki/xincheng213618/scgd_general_wpf");
        }
    }


}

