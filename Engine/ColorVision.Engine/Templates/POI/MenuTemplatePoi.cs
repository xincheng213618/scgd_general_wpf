using ColorVision.Engine.Templates.Menus;
using ColorVision.UI.Menus;

namespace ColorVision.Engine.Templates.POI
{
    public class MenuTemplatePoi : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override string Header => "关注点相关算法模版设置";
        public override int Order => 2;
    }

    public abstract class MenuTemplatePoiBase: MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuTemplatePoi);
    }


}
