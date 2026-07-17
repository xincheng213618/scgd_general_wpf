namespace ColorVision.Solution
{
    internal static class ImageResourceFileTypes
    {
        private static readonly HashSet<string> FusionExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp",
            ".jpeg",
            ".jpg",
            ".png",
            ".tif",
            ".tiff",
        };

        public static bool IsFusionCompatible(string extension)
        {
            return FusionExtensions.Contains(extension);
        }
    }
}
