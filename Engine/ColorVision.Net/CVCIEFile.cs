#pragma warning disable CA1051
namespace ColorVision.Net
{
    public enum CVType
    {
        None = -1,
        Raw,
        Src,
        CIE,
        Calibration,
        Tif,
        Dat
    }

    public struct CVCIEFile
    {
        public uint version;

        public CVType FileExtType;
        public  int rows;
        public int cols;
        public int bpp;
        public readonly int Depth
        {
            get
            {
                return bpp switch
                {
                    8 => 0,
                    16 => 2,
                    32 => 5,
                    64 => 6,
                    _ => 0,
                };
            }
        }
        public int channels;
        public float gain;
        public float[] exp;
        public string srcFileName;
        public byte[] data;

        public string FilePath { get; set; }
    }
}
