﻿namespace ColorVision.Engine.Templates.ImageCropping
{
    public class MenuImageCropping : MenuITemplateAlgorithmBase
    {
        public override string Header => "发光区裁剪";
        public override int Order => 1;
        public override ITemplate Template => new TemplateImageCropping();
    }
}
