namespace ColorVision.Engine.Templates.POI.POIOutput
{
    public class MenuPoiOutput : MenuTemplatePoiBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.POIFileOutputTemplateSettings;
        public override int Order => 4;

        public override ITemplate Template => new TemplatePoiOutputParam();
    }
}
