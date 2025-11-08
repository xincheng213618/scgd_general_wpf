using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.ImageCropping
{
    public class MenuImageCropping : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.AACropTemplateManagement;
        public override int Order => 41323;
        public override ITemplate Template => new TemplateImageCropping();
    }
}
