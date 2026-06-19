#pragma warning disable

namespace cvColorVision
{
    public class cvErrorDefine
    {
        public const int CV_ERR_SUCCESS = 1;
        public const int CV_ERR_UNKNOWN = -1;
    }

    public delegate void TiffShowEvent(string value, bool bfast);
    public delegate void LiveShowEvent(int w, int h, byte[] rawArray);
}
