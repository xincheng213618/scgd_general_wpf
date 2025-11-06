

using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.Distortion2
{
    public class MenuDisplayDistortion2 : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.DistortionDetection2_0;
        public override int Order => 1003;
        public override ITemplate Template => new TemplateDistortion2();
    }

}
