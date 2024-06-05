using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Algorithm.Dao;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    public class GhostResultData : ViewModelBase
    {
        public GhostResultData(int rows, int cols, string ghostPixelNum, string ghostPixels, string ledPixelNum, string ledPixels, string ledCenters, string ledBlobGray, string ghostAvrGray)
        {
            Rows = rows;
            Cols = cols;
            GhostPixelNum = ghostPixelNum;
            GhostPixels = ghostPixels;
            LedPixelNum = ledPixelNum;
            LedPixels = ledPixels;
            LedCenters = ledCenters;
            LedBlobGray = ledBlobGray;
            GhostAvrGray = ghostAvrGray;
        }
        public GhostResultData(AlgResultGhostModel algResultGhostModel)
        {
            Rows = algResultGhostModel.Rows;
            Cols = algResultGhostModel.Cols;
            GhostPixelNum = algResultGhostModel.SingleGhostPixelNum;
            GhostPixels = algResultGhostModel.GhostPixels;
            LedPixelNum = algResultGhostModel.SingleLedPixelNum;
            LedPixels = algResultGhostModel.LEDPixels;
            LedCenters = algResultGhostModel.LEDCenters;
            LedBlobGray = algResultGhostModel.LEDBlobGray;
            GhostAvrGray = algResultGhostModel.GhostAverageGray;
        }

        public int Rows { get; set; }
        public int Cols { get; set; }
        public string GhostPixelNum { get; set; }
        public string GhostPixels { get; set; }
        public string LedPixelNum { get; set; }
        public string LedPixels { get; set; }
        public string LedCenters { get; set; }
        public string LedBlobGray { get; set; }
        public string GhostAvrGray { get; set; }
    }
}
