#pragma warning disable CA1707
using ColorVision.Engine.Templates.Jsons.OLEDAOI;
using ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForBlackScreen;
using ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForQuardImg;
using ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForRePicGradingV2;
using ColorVision.Engine.Templates.Jsons.OLEDImageProcessing;

namespace ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration
{
    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmOLED_AOINode))]
    public class AlgorithmOLED_AOINodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmOLED_AOINode)context.Node;
            context.AddTemplateJsonPanel(nameof(node.TempName), "AOI", new TemplateOLEDAOI());
            context.AddTemplateJsonPanel(nameof(node.TempName), Properties.Resources.DefectCheckV2, new TemplateFPForRePicGradingV2());
            context.AddTemplateJsonPanel(nameof(node.TempName), Properties.Resources.BrightSpotCheck, new TemplateFPForQuardImg());
            context.AddTemplateJsonPanel(nameof(node.TempName), Properties.Resources.BlackScreenCheck, new TemplateFPForBlackScreen());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmOLEDImgNode))]
    public class AlgorithmOLEDImgNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmOLEDImgNode)context.Node;

            void Refresh()
            {
                context.SignStackPanel.Children.Clear();

                switch (node.Algorithm)
                {
                    case FlowEngineLib.Algorithm.AlgorithmOLEDImgType.局部图像增强:
                        context.AddTemplateJsonPanel(nameof(node.TempName), "局部图像增强", new TemplateLocalizationImageEnhancement());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmOLEDImgType.解串扰:
                        context.AddTemplateJsonPanel(nameof(node.TempName), "解串扰", new TemplateDediffusion());
                        break;
                }
            }

            context.ReconfigureOnPropertyChanged(node, nameof(node.Algorithm));
            Refresh();
        }
    }

}
