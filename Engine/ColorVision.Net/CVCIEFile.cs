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
        public int Depth
        {
            get
            {
                int num = bpp;
                if (1 == 0)
                {
                }
                int result;
                switch (num)
                {
                    case 8:
                        result = 0;
                        break;
                    case 16:
                        result = 2;
                        break;
                    case 32:
                        result = 5;
                        break;
                    case 64:
                        result = 6;
                        break;
                    default:
                        result = 0;
                        break;
                }
                if (1 == 0)
                {
                }
                return result;
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
