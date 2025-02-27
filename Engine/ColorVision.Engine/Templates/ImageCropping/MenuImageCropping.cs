namespace ColorVision.Engine.Templates.ImageCropping
{
    public class MenuImageCropping : MenuITemplateAlgorithmBase
    {
        public override string Header => "发光区裁剪模板管理";
        public override int Order => 41323;
        public override ITemplate Template => new TemplateImageCropping();
    }
}
