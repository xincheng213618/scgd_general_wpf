

using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.AAFindPoints
{
    public class MenuAAFindPoints : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.EmittingAreaLocation;
        public override int Order => 1003;
        public override ITemplate Template => new TemplateAAFindPoints();
    }

}
