using System.Collections.Generic;

namespace ColorVision.ImageEditor.Draw.Line
{
    /// <summary>
    /// Represents profile data extracted from an image along a line or polygon path.
    /// Supports both single-channel (grayscale) and multi-channel (RGB) data.
    /// </summary>
    public class ProfileData
    {
        /// <summary>
        /// Indicates if the source image is multi-channel (RGB).
        /// </summary>
        public bool IsMultiChannel { get; set; }

        /// <summary>
        /// Red channel values (for multi-channel images).
        /// </summary>
        public List<double> RedChannel { get; set; } = new List<double>();

        /// <summary>
        /// Green channel values (for multi-channel images).
        /// </summary>
        public List<double> GreenChannel { get; set; } = new List<double>();

        /// <summary>
        /// Blue channel values (for multi-channel images).
        /// </summary>
        public List<double> BlueChannel { get; set; } = new List<double>();

        /// <summary>
        /// Grayscale values (for single-channel images or computed from RGB).
        /// </summary>
        public List<double> GrayChannel { get; set; } = new List<double>();

        /// <summary>
        /// Number of sample points in the profile.
        /// </summary>
        public int SampleCount => GrayChannel.Count;

        /// <summary>
        /// Creates a ProfileData instance for single-channel (grayscale) image.
        /// </summary>
        public static ProfileData CreateSingleChannel(List<double> grayValues)
        {
            return new ProfileData
            {
                IsMultiChannel = false,
                GrayChannel = grayValues
            };
        }

        /// <summary>
        /// Creates a ProfileData instance for multi-channel (RGB) image.
        /// </summary>
        public static ProfileData CreateMultiChannel(List<double> redValues, List<double> greenValues, List<double> blueValues, List<double> grayValues)
        {
            return new ProfileData
            {
                IsMultiChannel = true,
                RedChannel = redValues,
                GreenChannel = greenValues,
                BlueChannel = blueValues,
                GrayChannel = grayValues
            };
        }
    }
}
