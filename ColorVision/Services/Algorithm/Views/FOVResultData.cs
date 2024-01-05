#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using MQTTMessageLib.Algorithm;

namespace ColorVision.Services.Algorithm.Views
{
    public class FOVResultData : ViewModelBase
    {
        public FovPattern Pattern { get; set; }

        public FovType Type { get; set; }

        public double Degrees { get; set; }

        public FOVResultData(FovPattern pattern, FovType type, double degrees)
        {
            Pattern = pattern;
            Type = type;
            Degrees = degrees;
        }

        public FOVResultData(AlgResultFOVModel algResultFOVModel)
        {
            Pattern = (FovPattern)algResultFOVModel.Pattern;
            Type = (FovType)algResultFOVModel.Type;
            Degrees = (double)algResultFOVModel.Degrees;
        }
    }
}
