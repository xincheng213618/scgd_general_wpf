
using ColorVision.MVVM;

namespace ColorVision.Services.Device.Algorithm.Views
{
    public class SFRResultData : ViewModelBase
    {
        public SFRResultData(float pdfrequency, float pdomainSamplingData)
        {
            this.pdfrequency = pdfrequency;
            this.pdomainSamplingData = pdomainSamplingData;
        }

        public float pdfrequency { get; set; }

        public float pdomainSamplingData { get; set; }
    }
}
