using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost
{
    public class ViewResultGhost : ViewModelBase, IViewResult
    {
        public ViewResultGhost(AlgResultGhostModel algResultGhostModel)
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
