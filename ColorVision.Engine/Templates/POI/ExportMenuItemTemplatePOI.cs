namespace ColorVision.Engine.Templates.POI
{
    public class ExportMenuItemTemplatePOI : ExportTemplateBase
    {
        public override string GuidId => "PoiParam";
        public override string Header => Properties.Resources.MenuPoi;
        public override int Order => 1;
        public override ITemplate Template { get; } = new TemplatePoi();
    }

}
