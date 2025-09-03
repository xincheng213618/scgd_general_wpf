#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using MQTTMessageLib.Algorithm;

namespace ColorVision.Engine.Templates.FOV
{
    public class ViewResultFOV : ViewModelBase, IViewResult
    {
        public FovPattern Pattern { get; set; }

        public FovType Type { get; set; }

        public double Degrees { get; set; }

        public ViewResultFOV(AlgResultFOVModel algResultFOVModel)
        {
            Pattern = (FovPattern)algResultFOVModel.Pattern;
            Type = (FovType)algResultFOVModel.Type;
            Degrees = (double)algResultFOVModel.Degrees;
        }
    }
}
