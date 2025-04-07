using ColorVision.Common.MVVM;

namespace CV_Spectrometer
{
    public class SpectralData:ViewModelBase
    {
        // 波长属性
        public double Wavelength { get; set; }
        // 相对光谱属性
        public double RelativeSpectrum { get; set; }
        // 绝对光谱属性
        public double AbsoluteSpectrum { get; set; }

    }

}
