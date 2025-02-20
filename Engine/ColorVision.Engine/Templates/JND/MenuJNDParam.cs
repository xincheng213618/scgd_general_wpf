namespace ColorVision.Engine.Templates.JND
{
    public class MenuJNDParam : MenuITemplateAlgorithmBase
    {
        public override string Header => "JND";
        public override int Order => 3;
        public override ITemplate Template => new TemplateJND();
    }
}
