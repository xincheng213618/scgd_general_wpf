

namespace ColorVision.Engine.Templates.Jsons.DFOV
{
    public class MenuDFOV : MenuITemplateAlgorithmBase
    {
        public override string Header => "DFOV";
        public override int Order => 1003;
        public override ITemplate Template => new TemplateDFOV();
    }

}
