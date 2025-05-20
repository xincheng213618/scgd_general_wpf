using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.SFR
{
    public class ExportSFRParam : MenuITemplateAlgorithmBase
    {
        public override string Header => "SFR";
        public override int Order => 1002;
        public override ITemplate Template => new TemplateSFR();
    }
}
