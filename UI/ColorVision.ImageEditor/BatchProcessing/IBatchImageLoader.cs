using OpenCvSharp;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.BatchProcessing
{
    /// <summary>
    /// Extends batch processing with application-specific image formats.
    /// Implementations must have a public parameterless constructor.
    /// </summary>
    public interface IBatchImageLoader
    {
        IReadOnlyCollection<string> Extensions { get; }

        Mat Load(string filePath);
    }
}
