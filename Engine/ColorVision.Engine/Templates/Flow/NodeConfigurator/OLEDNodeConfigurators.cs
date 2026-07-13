#pragma warning disable CA1707
using ColorVision.Engine.Properties;
using ColorVision.Engine.Templates.Jsons.OLEDAOI;
using ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForBlackScreen;
using ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForQuardImg;
using ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForRePicGradingV2;
using ColorVision.Engine.Templates.ImageCropping;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmOLED_AOINode))]
    public class AlgorithmOLED_AOINodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmOLED_AOINode)context.Node;
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "AOI", new TemplateOLEDAOI());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.DefectCheckV2, new TemplateFPForRePicGradingV2());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.BrightSpotCheck, new TemplateFPForQuardImg());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.BlackScreenCheck, new TemplateFPForBlackScreen());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.OLED.Algorithm2InNode))]
    public class Algorithm2InNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.OLED.Algorithm2InNode)context.Node;

            void Refresh()
            {
                context.SignStackPanel.Children.Clear();

                switch (node.Algorithm)
                {
                    case FlowEngineLib.Algorithm.Algorithm2Type.MTF:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "MTF2", new Jsons.MTF2.TemplateMTF2());
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "MTF", new MTF.TemplateMTF());
                        break;
                    case FlowEngineLib.Algorithm.Algorithm2Type.SFR:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "SFR", new SFR.TemplateSFR());
                        break;
                    case FlowEngineLib.Algorithm.Algorithm2Type.图像裁剪:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.ImageCrop, new TemplateImageCropping());
                        break;
                    case FlowEngineLib.Algorithm.Algorithm2Type.JND:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "JND", new JND.TemplateJND());
                        break;
                    case FlowEngineLib.Algorithm.Algorithm2Type.SFR_FindROI:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "SFR_FindROI", new Jsons.SFRFindROI.TemplateSFRFindROI());
                        break;
                    case FlowEngineLib.Algorithm.Algorithm2Type.十字计算:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.CrossCalc, new Jsons.FindCross.TemplateFindCross());
                        break;
                    default:
                        break;
                }
            }
            context.RebindNodeEvent(node, nameof(Algorithm2InNodeConfigurator), Refresh);
            Refresh();
        }
    }
}
