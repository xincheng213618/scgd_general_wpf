namespace ColorVision.Engine.Templates.FindLightArea
{
    public class ExportRoi : MenuITemplateAlgorithmBase
    {
        public override string Header => "发光区检测";
        public override int Order => 3;
        public override ITemplate Template => new TemplateRoi();
    }
}
