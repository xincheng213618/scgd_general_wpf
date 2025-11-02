using System;
using System.Diagnostics.CodeAnalysis;

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
    [SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Backward compatibility properties with lowercase names")]
    public class CVCIEFile
    {
        /// <summary>File format version.</summary>
        public uint Version { get; set; }

        /// <summary>File extension type.</summary>
        public CVType FileExtType { get; set; }
        
        /// <summary>Number of rows (height) in the image.</summary>
        public int Rows { get; set; }
        
        /// <summary>Number of columns (width) in the image.</summary>
        public int Cols { get; set; }
        
        /// <summary>Bits per pixel (8, 16, 32, or 64).</summary>
        public int Bpp { get; set; }
        
        /// <summary>
        /// Gets the OpenCV depth type based on bits per pixel.
        /// </summary>
        public int Depth
        {
            get
            {
                switch (Bpp)
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
        public int Channels { get; set; }
        
        /// <summary>Gain value applied to the image.</summary>
        public float Gain { get; set; }
        
        /// <summary>Exposure values for each channel.</summary>
        public float[] Exp { get; set; }
        
        /// <summary>Source file name or path.</summary>
        public string SrcFileName { get; set; }
        
        /// <summary>Raw image data bytes.</summary>
        public byte[] Data { get; set; }

        /// <summary>Full file path of the loaded file.</summary>
        public string FilePath { get; set; }

        // Backward compatibility: provide lowercase field-like access via properties
        /// <summary>File format version (deprecated - use Version).</summary>
        [Obsolete("Use Version property instead")]
        [SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Backward compatibility")]
        public uint version { get => Version; set => Version = value; }

        /// <summary>Number of rows (deprecated - use Rows).</summary>
        [Obsolete("Use Rows property instead")]
        [SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Backward compatibility")]
        public int rows { get => Rows; set => Rows = value; }

        /// <summary>Number of columns (deprecated - use Cols).</summary>
        [Obsolete("Use Cols property instead")]
        [SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Backward compatibility")]
        public int cols { get => Cols; set => Cols = value; }

        /// <summary>Bits per pixel (deprecated - use Bpp).</summary>
        [Obsolete("Use Bpp property instead")]
        [SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Backward compatibility")]
        public int bpp { get => Bpp; set => Bpp = value; }

        /// <summary>Number of channels (deprecated - use Channels).</summary>
        [Obsolete("Use Channels property instead")]
        [SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Backward compatibility")]
        public int channels { get => Channels; set => Channels = value; }

        /// <summary>Gain value (deprecated - use Gain).</summary>
        [Obsolete("Use Gain property instead")]
        [SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Backward compatibility")]
        public float gain { get => Gain; set => Gain = value; }

        /// <summary>Exposure values (deprecated - use Exp).</summary>
        [Obsolete("Use Exp property instead")]
        [SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Backward compatibility")]
        public float[] exp { get => Exp; set => Exp = value; }

        /// <summary>Source file name (deprecated - use SrcFileName).</summary>
        [Obsolete("Use SrcFileName property instead")]
        [SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Backward compatibility")]
        public string srcFileName { get => SrcFileName; set => SrcFileName = value; }

        /// <summary>Raw data (deprecated - use Data).</summary>
        [Obsolete("Use Data property instead")]
        [SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Backward compatibility")]
        public byte[] data { get => Data; set => Data = value; }
    }
}
