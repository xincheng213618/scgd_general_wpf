using static cvColorVision.GCSDLL;

namespace ColorVision.Services.Devices.Spectrum.Configs
{
    public class SpectrumData
    {
        public int ID { get; set; }
        public float V { get; set; }
        public float I { get; set; }
        public ColorParam Data { get; set; }

        public SpectrumData(int id, ColorParam data)
        {
            ID = id;
            Data = data;
            V = float.NaN;
            I = float.NaN;
        }

        public SpectrumData()
        {
            V = float.NaN;
            I = float.NaN;
        }
    }
}
