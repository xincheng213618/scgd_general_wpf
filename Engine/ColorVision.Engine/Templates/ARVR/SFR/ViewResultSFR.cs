using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Algorithm;

namespace ColorVision.Engine.Templates.SFR
{
    public class ViewResultSFR : ViewModelBase, IViewResult
    {

        public ViewResultSFR(float pdfrequency, float pdomainSamplingData)
        {
            this.pdfrequency = pdfrequency;
            this.pdomainSamplingData = pdomainSamplingData;
        }
        public float pdfrequency { get; set; }
        public float pdomainSamplingData { get; set; }

    }
}
