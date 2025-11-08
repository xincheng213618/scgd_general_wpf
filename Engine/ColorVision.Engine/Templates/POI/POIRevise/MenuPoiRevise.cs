namespace ColorVision.Engine.Templates.POI.POIRevise
{
    public class MenuPoiRevise : MenuTemplatePoiBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.POICorrectionTemplateSettings;
        public override int Order => 3;
        public override ITemplate Template => new TemplatePoiReviseParam();
    }
}
