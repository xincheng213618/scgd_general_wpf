using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.Jsons.LedCheck2;
using ColorVision.Engine.Templates.Jsons.CompoundImg;
using ColorVision.Engine.Templates.Jsons.OLEDAOI;
using ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForBlackScreen;
using ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForQuardImg;
using ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForRePicGradingV2;
using ColorVision.Engine.Templates.ImageCropping;
using ColorVision.Engine.Templates.POI.POIOutput;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [NodeConfigurator(typeof(FlowEngineLib.Node.OLED.OLEDRebuildPixelsNode))]
    public class OLEDRebuildPixelsNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.OLED.OLEDRebuildPixelsNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "亚像素灯珠检测", new TemplateLedCheck2());
            context.AddTemplatePanel(name => node.OutputTemplateName = name, node.OutputTemplateName, "PoiOutPut", new TemplatePoiOutputParam());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.OLED.OLEDImageCroppingNode))]
    public class OLEDImageCroppingNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.OLED.OLEDImageCroppingNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, "参数模板", new TemplateImageCropping());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.OLED.AlgorithmCompoundImgNode))]
    public class AlgorithmCompoundImgNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.OLED.AlgorithmCompoundImgNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "参数模板", new TemplateCompoundImg());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmOLEDNode))]
    public class AlgorithmOLEDNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmOLEDNode)context.Node;
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "亚像素", new TemplateLedCheck2());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmOLED_AOINode))]
    public class AlgorithmOLED_AOINodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmOLED_AOINode)context.Node;
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "AOI", new TemplateOLEDAOI());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "缺陷检测V2", new TemplateFPForRePicGradingV2());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "亮点检测", new TemplateFPForQuardImg());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "黑画面检测", new TemplateFPForBlackScreen());
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
                context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());

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
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "图像裁剪", new TemplateImageCropping());
                        break;
                    case FlowEngineLib.Algorithm.Algorithm2Type.JND:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "JND", new JND.TemplateJND());
                        break;
                    case FlowEngineLib.Algorithm.Algorithm2Type.SFR_FindROI:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "SFR_FindROI", new Jsons.SFRFindROI.TemplateSFRFindROI());
                        break;
                    case FlowEngineLib.Algorithm.Algorithm2Type.十字计算:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "十字计算", new Jsons.FindCross.TemplateFindCross());
                        break;
                    default:
                        break;
                }
            }
            node.nodeEvent -= (s, e) => Refresh();
            node.nodeEvent += (s, e) => Refresh();
            Refresh();
        }
    }
}
