
using ColorVision.Common.MVVM;

namespace ColorVision.Services.Devices.Algorithm.Views
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
