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
        /// <summary>Raw image Data format.</summary>
        Raw,
        /// <summary>Source image format.</summary>
        Src,
        /// <summary>CIE color space Data format.</summary>
        CIE,
        /// <summary>Calibration Data format.</summary>
        Calibration,
        /// <summary>TIFF image format.</summary>
        Tif,
        /// <summary>Data file format.</summary>
        Dat
    }

    /// <summary>
    /// Represents a ColorVision CIE file structure containing image metadata and Data.
    /// Used for CVCIE, CVRAW, and CVSRC file formats.
    /// Implements IDisposable for proper resource management.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Backward compatibility properties with lowercase names")]
    public class CVCIEFile : IDisposable
    {
        /// <summary>File format Version.</summary>
        public uint Version { get; set; }

        /// <summary>File extension type.</summary>
        public CVType FileExtType { get; set; }
        
        /// <summary>Number of Rows (height) in the image.</summary>
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
        
        /// <summary>Number of color Channels in the image.</summary>
        public int Channels { get; set; }
        
        /// <summary>Gain value applied to the image.</summary>
        public float Gain { get; set; }
        
        /// <summary>Exposure values for each channel.</summary>
        public float[] Exp { get; set; }
        
        /// <summary>Source file name or path.</summary>
        public string SrcFileName { get; set; }
        
        /// <summary>Raw image Data bytes.</summary>
        public byte[] Data { get; set; }

        /// <summary>Full file path of the loaded file.</summary>
        public string FilePath { get; set; }

        // IDisposable implementation
        private bool _disposed;

        /// <summary>
        /// Releases the resources used by the CVCIEFile.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the CVCIEFile and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clear large Data arrays to help GC
                    Data = null;
                    Exp = null;
                }
                _disposed = true;
            }
        }
    }
}
