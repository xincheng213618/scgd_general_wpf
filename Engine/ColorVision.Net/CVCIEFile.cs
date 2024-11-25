#pragma warning disable CA1806,CA1833,CA1401,CA2101,CA1838,CS8603,CA1051,CA1707,CS8625
using MQTTMessageLib.FileServer;


namespace ColorVision.Net
{
    public struct CVCIEFile
    {
        public uint version;

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

        public string FilePath { get; set; }
    }
}
