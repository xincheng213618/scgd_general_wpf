using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.POI
{
    public class MenuItemPoiParam : MenuItemTemplateBase
    {
        public override string Header => "关注点模板";
        public override int Order => 1;
        public override ITemplate Template { get; } = new TemplatePoi();
    }

}
