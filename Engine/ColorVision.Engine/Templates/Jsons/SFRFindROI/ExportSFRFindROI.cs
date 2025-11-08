using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.SFRFindROI
{
    public class ExportSFRFindROI : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.SFRFindRoi;
        public override int Order => 2;
        public override ITemplate Template => new TemplateSFRFindROI();
    }
}
