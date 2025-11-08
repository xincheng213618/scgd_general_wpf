using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.CompoundImg
{
    public class MenuCompoundImg : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.ImageStitching;
        public override int Order => 1004;
        public override ITemplate Template => new TemplateCompoundImg();
    }

}
