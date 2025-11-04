using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.SFR2
{
    public class MenuSFR2 : MenuITemplateAlgorithmBase
    {
        public override string Header => "SFR2.0";
        public override int Order => 1004;
        public override ITemplate Template => new TemplateSFR2();
    }

}
