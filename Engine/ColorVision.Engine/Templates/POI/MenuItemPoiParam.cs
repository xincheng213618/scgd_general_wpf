namespace ColorVision.Engine.Templates.POI
{
    public class MenuItemPoiParam : MenuItemTemplateBase
    {
        public override string GuidId => nameof(MenuTemplatePoi);
        public override string Header => Properties.Resources.MenuPoi;
        public override int Order => 1;
        public override ITemplate Template { get; } = new TemplatePoi();
    }

}
