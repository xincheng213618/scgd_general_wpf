#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;

namespace ColorVision.Services.Algorithm.Views
{
    public class DistortionResultData : ViewModelBase
    {
        public DistortionResultData(DistortionType disType, DisSlopeType slopeType, DisLayoutType layoutType, DisCornerType cornerType, double maxRatio, PointFloat[] finalPoint)
        {
            DisType = disType;
            SlopeType = slopeType;
            LayoutType = layoutType;
            CornerType = cornerType;
            MaxRatio = maxRatio;
            FinalPoints = finalPoint;
        }

        public DistortionResultData(AlgResultDistortionModel algResultDistortionModel)
        {
            DisType = (DistortionType)algResultDistortionModel.Type;
            SlopeType = (DisSlopeType)algResultDistortionModel.SlopeType;
            LayoutType = (DisLayoutType)algResultDistortionModel.LayoutType;
            CornerType = (DisCornerType)algResultDistortionModel.CornerType;
            MaxRatio = (double)algResultDistortionModel.MaxRatio;
            FinalPoints = JsonConvert.DeserializeObject<PointFloat[]>(algResultDistortionModel.FinalPoints);
        }

        public DistortionType DisType { get; set; }
        public string DisTypeDesc
        {
            get
            {
                string result = DisType.ToString();
                switch (DisType)
                {
                    case DistortionType.OpticsDist:
                        result = DisType.ToString();
                        break;
                    case DistortionType.TVDistH:
                        result = DisType.ToString();
                        break;
                    case DistortionType.TVDistV:
                        result = DisType.ToString();
                        break;
                    default:
                        result = DisType.ToString();
                        break;
                }
                return result;
            }
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
