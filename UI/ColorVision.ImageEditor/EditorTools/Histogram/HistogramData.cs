namespace ColorVision.ImageEditor.EditorTools.Histogram
{
    /// <summary>
    /// Represents histogram data for single-channel (grayscale) or multi-channel (RGB) images.
    /// </summary>
    public class HistogramData
    {
        /// <summary>
        /// Indicates if the histogram is for a multi-channel (RGB) image.
        /// </summary>
        public bool IsMultiChannel { get; set; }

        /// <summary>
        /// Red channel histogram (256 bins).
        /// </summary>
        public int[] RedChannel { get; set; } = new int[256];

        /// <summary>
        /// Green channel histogram (256 bins).
        /// </summary>
        public int[] GreenChannel { get; set; } = new int[256];

        /// <summary>
        /// Blue channel histogram (256 bins).
        /// </summary>
        public int[] BlueChannel { get; set; } = new int[256];

        /// <summary>
        /// Grayscale histogram (256 bins).
        /// </summary>
        public int[] GrayChannel { get; set; } = new int[256];

        /// <summary>
        /// Creates a HistogramData instance for single-channel (grayscale) image.
        /// </summary>
        public static HistogramData CreateSingleChannel(int[] grayHistogram)
        {
            return new HistogramData
            {
                IsMultiChannel = false,
                GrayChannel = grayHistogram,
                RedChannel = new int[256],
                GreenChannel = new int[256],
                BlueChannel = new int[256]
            };
        }

        /// <summary>
        /// Creates a HistogramData instance for multi-channel (RGB) image.
        /// </summary>
        public static HistogramData CreateMultiChannel(int[] redHistogram, int[] greenHistogram, int[] blueHistogram)
        {
            return new HistogramData
            {
                IsMultiChannel = true,
                RedChannel = redHistogram,
                GreenChannel = greenHistogram,
                BlueChannel = blueHistogram,
                GrayChannel = new int[256]
            };
        }
    }
}
