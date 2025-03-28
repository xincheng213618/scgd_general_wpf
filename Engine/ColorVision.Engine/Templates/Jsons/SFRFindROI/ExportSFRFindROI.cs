namespace ColorVision.Engine.Templates.Jsons.SFRFindROI
{
    public class ExportSFRFindROI : MenuITemplateAlgorithmBase
    {
        public override string Header => "SFR寻边";
        public override int Order => 2;
        public override ITemplate Template => new TemplateSFRFindROI();
    }
}
