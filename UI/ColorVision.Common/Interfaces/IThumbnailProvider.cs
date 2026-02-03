using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.UI
{
    /// <summary>
    /// Interface for thumbnail generation providers.
    /// Implementations can handle custom file formats like cvraw/cvcie that require special processing (e.g., OpenCV).
    /// Use [FileExtension] attribute to specify supported file extensions.
    /// </summary>
    public interface IThumbnailProvider
    {
        /// <summary>
        /// Priority order for the provider. Lower values are checked first.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Checks if this provider can handle the specified file.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if this provider can generate a thumbnail for the file</returns>
        bool CanHandle(string filePath);

        /// <summary>
        /// Generates a thumbnail for the specified file.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="maxSize">Maximum dimension (width or height) of the thumbnail</param>
        /// <returns>BitmapSource thumbnail, or null if generation fails</returns>
        Task<BitmapSource?> GenerateThumbnailAsync(string filePath, int maxSize);

        /// <summary>
        /// Gets the original image dimensions without fully loading the image.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Tuple of (width, height), or (0, 0) if dimensions cannot be determined</returns>
        (int width, int height) GetImageDimensions(string filePath);
    }
}
