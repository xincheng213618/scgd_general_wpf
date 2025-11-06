namespace ColorVision.Engine.Templates.POI.POIGenCali
{
    public class MenuPoiGenCali : MenuTemplatePoiBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.POICalibrationCorrectionTemplateSettings;
        public override int Order => 2;
        public override ITemplate Template => new TemplatePoiGenCalParam();
    }
}
