using ColorVision.Engine.Properties;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.DataLoad;
using ColorVision.Engine.Templates.Distortion;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.FocusPoints;
using ColorVision.Engine.Templates.FOV;
using ColorVision.Engine.Templates.Ghost;
using ColorVision.Engine.Templates.ImageCropping;
using ColorVision.Engine.Templates.JND;
using ColorVision.Engine.Templates.Jsons.AAFindPoints;
using ColorVision.Engine.Templates.Jsons.BinocularFusion;
using ColorVision.Engine.Templates.Jsons.BlackMura;
using ColorVision.Engine.Templates.Jsons.CaliAngleShift;
using ColorVision.Engine.Templates.Jsons.CompoundImg;
using ColorVision.Engine.Templates.Jsons.Distortion2;
using ColorVision.Engine.Templates.Jsons.FindCross;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.Engine.Templates.Jsons.Ghost2;
using ColorVision.Engine.Templates.Jsons.ImageROI;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.Jsons.LedCheck2;
using ColorVision.Engine.Templates.Jsons.LEDStripDetectionV2;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.Jsons.SFRFindROI;
using ColorVision.Engine.Templates.LedCheck;
using ColorVision.Engine.Templates.LEDStripDetection;
using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.SFR;
using ColorVision.Engine.Templates.Validate;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmCaliNode))]
    public class AlgorithmCaliNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmCaliNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.ColorDifference, new TemplateCaliAngleShift());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmFindLightAreaNode))]
    public class AlgorithmFindLightAreaNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmFindLightAreaNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.FindAAArea, new TemplateAAFindPoints());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.LightAreaLocation, new TemplateRoi());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, "FocusPoints", new TemplateFocusPoints());
            context.AddTemplatePanel(name => node.SavePOITempName = name, node.SavePOITempName, Properties.Resources.SavePOI, new TemplatePoi());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmFindLEDNode))]
    public class AlgorithmFindLEDNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmFindLEDNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.SubPixelLedCheck, new TemplateLedCheck2());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.PixelLedCheck, new TemplateLedCheck());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmGhostV2Node))]
    public class AlgorithmGhostV2NodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmGhostV2Node)context.Node;
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "GhostQK", new TemplateGhostQK());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, "Ghost", new TemplateGhost());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmBlackMuraNode))]
    public class AlgorithmBlackMuraNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmBlackMuraNode)context.Node;
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "BlackMura", new TemplateBlackMura());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmKBNode))]
    public class AlgorithmKBNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmKBNode)context.Node;
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplateKBPanel(name => node.TempName = name, node.TempName, "KB", new TemplateKB());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmKBOutputNode))]
    public class AlgorithmKBOutputNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmKBOutputNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplateKBPanel(name => node.TempName = name, node.TempName, "KB", new TemplateKB());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgorithmImageROINode))]
    public class AlgorithmImageROINodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgorithmImageROINode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
            context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.TemplateName, new TemplateImageROI());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgDataLoadNode))]
    public class AlgDataLoadNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgDataLoadNode)context.Node;
            context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
            context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.Template, new TemplateDataLoad());
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Algorithm.AlgorithmARVRNode))]
    public class AlgorithmARVRNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Algorithm.AlgorithmARVRNode)context.Node;

            void Refresh()
            {
                context.SignStackPanel.Children.Clear();
                context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
                context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);

                switch (node.Algorithm)
                {
                    case FlowEngineLib.Algorithm.AlgorithmARVRType.MTF:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "MTF", new TemplateMTF());
                        context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, "POI", new TemplatePoi());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmARVRType.SFR:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "SFR", new TemplateSFR());
                        context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, "POI", new TemplatePoi());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmARVRType.FOV:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "DFOV", new TemplateDFOV());
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "FOV", new TemplateFOV());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmARVRType.畸变:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.Distortion, new TemplateDistortion2());
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.Distortion, new TemplateDistortionParam());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmARVRType.SFR_FindROI:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "SFR_FindROI", new TemplateSFRFindROI());
                        context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, "POI", new TemplatePoi());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmARVRType.双目融合:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.BinocularFusion, new TemplateBinocularFusion());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmARVRType.十字计算:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.CrossCalc, new TemplateFindCross());
                        context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, "ROI", new TemplatePoi());
                        break;
                    default:
                        break;
                }
            }
            context.RebindNodeEvent(node, nameof(AlgorithmARVRNodeConfigurator), Refresh);
            Refresh();
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Algorithm.AlgorithmNode))]
    public class AlgorithmNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Algorithm.AlgorithmNode)context.Node;

            void Refresh()
            {
                context.SignStackPanel.Children.Clear();
                context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
                context.AddImagePath(name => node.ImgFileName = name, node.ImgFileName);
                context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, "POI", new TemplatePoi());

                switch (node.Algorithm)
                {
                    case FlowEngineLib.Algorithm.AlgorithmType.MTF:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "MTF", new TemplateMTF());
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "MTF2", new TemplateMTF2());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.SFR:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "SFR", new TemplateSFR());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.FOV:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "DFOV", new TemplateDFOV());
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "FOV", new TemplateFOV());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.鬼影:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "GhostQK", new TemplateGhostQK());
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "Ghost", new TemplateGhost());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.畸变:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "Distortion2", new TemplateDistortion2());
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "Distortion", new TemplateDistortionParam());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.灯珠检测:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.LedCheck, new TemplateLedCheck());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.灯带检测:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.LedStripCheckV2, new TemplateLEDStripDetectionV2());
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.LedStripCheck, new TemplateLEDStripDetection());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.发光区检测:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.FindLightArea, new TemplateFocusPoints());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.发光区检测OLED:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.FindLightAreaOLED, new TemplateRoi());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.JND:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "JND", new TemplateJND());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.SFR_FindROI:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "SFR_FindROI", new TemplateSFRFindROI());
                        context.AddTemplatePanel(name => node.POITempName = name, node.POITempName, "POI", new TemplatePoi());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.双目融合:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.BinocularFusion, new TemplateBinocularFusion());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.AA布点:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.AABuildPoints, new TemplateAAFindPoints());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.图像裁剪:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, Properties.Resources.ImageCrop, new TemplateImageCropping());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.ImageCompound:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, "ImageCompound", new TemplateCompoundImg());
                        break;
                    case FlowEngineLib.Algorithm.AlgorithmType.十字计算:
                        context.AddTemplateJsonPanel(name => node.TempName = name, node.TempName, Properties.Resources.CrossCalc, new TemplateFindCross());
                        break;
                    default:
                        break;
                }
            }
            context.RebindNodeEvent(node, nameof(AlgorithmNodeConfigurator), Refresh);
            Refresh();
        }
    }

    [NodeConfigurator(typeof(FlowEngineLib.Node.Algorithm.AlgComplianceMathNode))]
    public class AlgComplianceMathNodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.Node.Algorithm.AlgComplianceMathNode)context.Node;

            void Refresh()
            {
                context.SignStackPanel.Children.Clear();
                context.AddDevicePanel(name => node.DeviceCode = name, node.DeviceCode, "", ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList());
                switch (node.ComplianceMath)
                {
                    case FlowEngineLib.Node.Algorithm.ComplianceMathType.CIE:
                        context.AddTemplateCollectionPanel(name => node.TempName = name, node.TempName, "CIE", new ObservableCollection<TemplateModel<ValidateParam>>(TemplateComplyParam.CIEParams.SelectMany(p => p.Value)));
                        break;
                    case FlowEngineLib.Node.Algorithm.ComplianceMathType.JND:
                        context.AddTemplatePanel(name => node.TempName = name, node.TempName, "JND", new TemplateComplyParam("Comply.JND"));
                        break;
                    default:
                        break;
                }
            }
            context.RebindNodeEvent(node, nameof(AlgComplianceMathNodeConfigurator), Refresh);
            Refresh();
        }
    }
}
