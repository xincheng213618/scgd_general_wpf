

using cvColorVision;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{
    public class SpectrumData
    {
        public int ID { get; set; }
        public float V { get; set; }
        public float I { get; set; }
        public COLOR_PARA Data { get; set; }

        public SpectrumData(int id, COLOR_PARA data)
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
