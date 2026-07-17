using System.ComponentModel;

namespace Spectrum
{
    public enum SpectrometerType
    {
        [Description("SP100")]
        CMvSpectra = 0,
        [Description("SP10")]
        LightModule = 1,
        [Description("高利通")]
        Gaolitong = 2,
    }
}
