using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.POI
{
    public class MenuItemPoiParam : MenuItemTemplateBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.PointOfInterestTemplete;
        public override int Order => 1;
        public override ITemplate Template { get; } = new TemplatePoi();
    }

}
