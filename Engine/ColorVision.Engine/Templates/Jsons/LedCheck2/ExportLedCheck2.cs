namespace ColorVision.Engine.Templates.Jsons.LedCheck2
{
    public class ExportLedCheck2 : MenuITemplateAlgorithmBase
    {
        public override string Header => "灯珠检测2";
        public override int Order => 2;
        public override ITemplate Template => new TemplateLedCheck2();
    }
}
