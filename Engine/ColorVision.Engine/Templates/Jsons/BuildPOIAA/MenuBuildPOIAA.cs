

using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.BuildPOIAA
{
    public class MenuBuildPOIAA : MenuITemplateAlgorithmBase
    {
        public override string Header => "AA补点";
        public override int Order => 1003;
        public override ITemplate Template => new TemplateBuildPOIAA();
    }

}
