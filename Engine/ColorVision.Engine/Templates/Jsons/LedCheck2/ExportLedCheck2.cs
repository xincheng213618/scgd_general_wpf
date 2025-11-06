using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.LedCheck2
{
    public class ExportLedCheck2 : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.subPixelDetect;
        public override int Order => 41325;
        public override ITemplate Template => new TemplateLedCheck2();

    }
}
