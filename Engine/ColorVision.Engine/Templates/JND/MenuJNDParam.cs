using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.JND
{
    public class MenuJNDParam : MenuITemplateAlgorithmBase
    {
        public override string Header => "JND";
        public override int Order => 2003;
        public override ITemplate Template => new TemplateJND();
    }
}
