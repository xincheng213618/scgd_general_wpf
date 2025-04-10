#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Engine.Interfaces;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;

namespace ColorVision.Engine.Templates.Distortion
{
    public class ViewResultDistortion : ViewModelBase, IViewResult
    {
        public ViewResultDistortion(AlgResultDistortionModel algResultDistortionModel)
        {
            DisType = algResultDistortionModel.Type;
            SlopeType = algResultDistortionModel.SlopeType;
            LayoutType = algResultDistortionModel.LayoutType;
            CornerType = algResultDistortionModel.CornerType;
            MaxRatio = (double)algResultDistortionModel.MaxRatio;
            FinalPoints = JsonConvert.DeserializeObject<PointFloat[]>(algResultDistortionModel.FinalPoints) ?? Array.Empty<PointFloat>();
        }

        public DistortionType DisType { get; set; }
        public string DisTypeDesc
        {
            get => DisType.ToString();
        }
        public DisSlopeType SlopeType { get; set; }

        public string SlopeTypeDesc
        {
            get
            {
                string result = SlopeType.ToString();
                switch (SlopeType)
                {
                    case DisSlopeType.CenterPoint:
                        result = "中心九点";
                        break;
                    case DisSlopeType.lb_Variance:
                        result = "方差去除";
                        break;
                    default:
                        result = SlopeType.ToString();
                        break;
                }
                return result;
            }
        }
        public DisLayoutType LayoutType { get; set; }

        public string LayoutTypeDesc
        {
            get
            {
                string result = LayoutType.ToString();
                switch (LayoutType)
                {
                    case DisLayoutType.SlopeIN:
                        result = "斜率布点";
                        break;
                    case DisLayoutType.SlopeOUT:
                        result = "非斜率布点";
                        break;
                    default:
                        result = LayoutType.ToString();
                        break;
                }
                return result;
            }
        }
        public DisCornerType CornerType { get; set; }

        public string CornerTypeDesc
        {
            get
            {
                string result = CornerType.ToString();
                switch (CornerType)
                {
                    case DisCornerType.Circlepoint:
                        result = "圆点";
                        break;
                    case DisCornerType.Checkerboard:
                        result = "棋盘格";
                        break;
                    default:
                        result = CornerType.ToString();
                        break;
                }
                return result;
            }
        }
        public double MaxRatio { get; set; }
        public PointFloat[] FinalPoints { get; set; }
    }
}
