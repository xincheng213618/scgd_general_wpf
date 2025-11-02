namespace ColorVision.FileIO
{
    /// <summary>
    /// ColorVision file type enumeration.
    /// </summary>
    public enum CVType
    {
        /// <summary>No specific type.</summary>
        None = -1,
        /// <summary>Raw image data format.</summary>
        Raw,
        /// <summary>Source image format.</summary>
        Src,
        /// <summary>CIE color space data format.</summary>
        CIE,
        /// <summary>Calibration data format.</summary>
        Calibration,
        /// <summary>TIFF image format.</summary>
        Tif,
        /// <summary>Data file format.</summary>
        Dat
    }

    /// <summary>
    /// Represents a ColorVision CIE file structure containing image metadata and data.
    /// Used for CVCIE, CVRAW, and CVSRC file formats.
    /// </summary>
    public struct CVCIEFile
    {
        /// <summary>File format version.</summary>
        public uint version;

        /// <summary>File extension type.</summary>
        public CVType FileExtType;
        
        /// <summary>Number of rows (height) in the image.</summary>
        public  int rows;
        
        /// <summary>Number of columns (width) in the image.</summary>
        public int cols;
        
        /// <summary>Bits per pixel (8, 16, 32, or 64).</summary>
        public int bpp;
        /// <summary>
        /// Gets the OpenCV depth type based on bits per pixel.
        /// </summary>
        public int Depth
        {
            get
            {
                switch (bpp)
                {
                    case 8:
                        return 0;  // CV_8U
                    case 16:
                        return 2;  // CV_16U
                    case 32:
                        return 5;  // CV_32F
                    case 64:
                        return 6;  // CV_64F
                    default:
                        return 0;  // Default to CV_8U
                }
            }
        }
        
        /// <summary>Number of color channels in the image.</summary>
        public int channels;
        
        /// <summary>Gain value applied to the image.</summary>
        public float gain;
        
        /// <summary>Exposure values for each channel.</summary>
        public float[] exp;
        
        /// <summary>Source file name or path.</summary>
        public string srcFileName;
        
        /// <summary>Raw image data bytes.</summary>
        public byte[] data;

        /// <summary>Full file path of the loaded file.</summary>
        public string FilePath { get; set; }
    }
}
