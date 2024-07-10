using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.SFR
{
    public class ViewResultSFR : ViewModelBase
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
