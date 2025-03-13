namespace ColorVision.Engine.Templates.LedCheck
{
    public class ExportLedCheckParam : MenuITemplateAlgorithmBase
    {
        public override string Header => "像素级灯珠检测";
        public override int Order => 41324;
        public override ITemplate Template => new TemplateLedCheck();
    }
}
