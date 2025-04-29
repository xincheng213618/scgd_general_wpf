

namespace ColorVision.Engine.Templates.Jsons.FOV2
{
    public class MenuFOV2 : MenuITemplateAlgorithmBase
    {
        public override string Header => "FOV_2.0";
        public override int Order => 1003;
        public override ITemplate Template => new TemplateDFOV();
    }

}
