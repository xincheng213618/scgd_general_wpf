using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.ARVR.AOI
{
    public class MenuAOIParam : MenuITemplateAlgorithmBase
    {
        public override string Header => "AOI";
        public override int Order => 13;
        public override ITemplate Template => new TemplateAOIParam();
    }
}
