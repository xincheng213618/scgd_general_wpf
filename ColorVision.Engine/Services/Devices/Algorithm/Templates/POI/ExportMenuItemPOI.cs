using ColorVision.UI.Menus;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI
{
    public class ExportMenuItemPOI : MenuItemBase
    {
        public override string OwnerGuid => "Template";
        public override string GuidId => "TemplatePOI";
        public override string Header => "关注点相关算法模板设置";
        public override int Order => 2;
    }


}
