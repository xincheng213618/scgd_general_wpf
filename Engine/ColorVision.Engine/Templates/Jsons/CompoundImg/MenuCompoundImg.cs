using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.CompoundImg
{
    public class MenuCompoundImg : MenuITemplateAlgorithmBase
    {
        public override string Header => "图像拼接";
        public override int Order => 1004;
        public override ITemplate Template => new TemplateCompoundImg();
    }

}
