namespace ColorVision.Engine.Templates.LedCheck
{
    public class ExportLedCheckParam : MenuITemplateAlgorithmBase
    {
        public override string Header => "灯珠检测1";
        public override int Order => 2;
        public override ITemplate Template => new TemplateLedCheck();
    }
}
