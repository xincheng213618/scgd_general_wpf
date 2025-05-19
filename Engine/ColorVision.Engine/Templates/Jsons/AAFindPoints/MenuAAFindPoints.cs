

namespace ColorVision.Engine.Templates.Jsons.AAFindPoints
{
    public class MenuAAFindPoints : MenuITemplateAlgorithmBase
    {
        public override string Header => "寻找AA区";
        public override int Order => 1003;
        public override ITemplate Template => new TemplateAAFindPoints();
    }

}
