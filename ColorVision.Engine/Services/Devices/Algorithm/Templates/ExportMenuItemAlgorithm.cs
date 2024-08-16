using ColorVision.UI.Menus;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates
{
    public class ExportMenuItemAlgorithm : MenuItemBase
    {
        public override string OwnerGuid => "Template";
        public override string GuidId => "TemplateAlgorithm";
        public override string Header => Properties.Resources.MenuAlgorithm;
        public override int Order => 2;
    }

    public class ExportMenuItemPOI : MenuItemBase
    {
        public override string OwnerGuid => "Template";
        public override string GuidId => "TemplatePOI";
        public override string Header => "POI相关模板";
        public override int Order => 2;
    }


}
