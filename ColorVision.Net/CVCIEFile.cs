#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838,CS8603,CA1051,CA1707,CS8625
using MQTTMessageLib.FileServer;


namespace ColorVision.Net
{
    public struct CVCIEFile
    {
        public FileExtType FileExtType;
        public int rows;
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
        public int gain;
        public float[] exp;
        public string srcFileName;
        public byte[] data;

        public CVCIEFile(CVCIEFile info)
        {
            FileExtType = info.FileExtType;
            rows = info.rows;
            cols = info.cols;
            bpp = info.bpp;
            channels = info.channels;
            gain = info.gain;
            exp = info.exp;
            srcFileName = info.srcFileName;
            data = info.data;
        }
    }
}
