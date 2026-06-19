namespace ColorVision.ImageEditor
{
    public enum ImageViewPropertyScope
    {
        ImageMetadata,
        ViewState,
        OpenerRuntime,
    }

    public sealed class ImageViewPropertyEntry
    {
        public required string Key { get; init; }
        public object? Value { get; init; }
        public ImageViewPropertyScope Scope { get; init; }
        public string? Owner { get; init; }
        public string? Description { get; init; }
    }

    public static class ImageViewPropertyKeys
    {
        public const string FilePath = "FilePath";
        public const string FileSource = "FileSource";
        public const string FileName = "FileName";
        public const string FileSize = "FileSize";
        public const string FileCreationTime = "FileCreationTime";
        public const string FileModifiedTime = "FileModifiedTime";
        public const string ImageWidth = "ImageWidth";
        public const string ImageHeight = "ImageHeight";
        public const string PixelFormat = "PixelFormat";
        public const string Cols = "Cols";
        public const string Rows = "Rows";
        public const string Channel = "Channel";
        public const string Depth = "Depth";
        public const string Stride = "Stride";
        public const string DpiX = "DpiX";
        public const string DpiY = "DpiY";
        public const string CameraModel = "CameraModel";
        public const string CameraManufacturer = "CameraManufacturer";
        public const string DateTaken = "DateTaken";
        public const string ApplicationName = "ApplicationName";
        public const string ImageTitle = "ImageTitle";
        public const string ImageSubject = "ImageSubject";
        public const string VideoWidth = "VideoWidth";
        public const string VideoHeight = "VideoHeight";
        public const string VideoFPS = "VideoFPS";
        public const string VideoTotalFrames = "VideoTotalFrames";
        public const string VideoDuration = "VideoDuration";
    }
}
