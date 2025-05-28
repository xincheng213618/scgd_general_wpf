using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.MTF2
{
    public class MenuMTF2 : MenuITemplateAlgorithmBase
    {
        public override string Header => "MTF2.0";
        public override int Order => 1003;
        public override ITemplate Template => new TemplateMTF2();
    }

}
