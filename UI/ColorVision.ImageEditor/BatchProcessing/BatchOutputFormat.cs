namespace ColorVision.ImageEditor.BatchProcessing
{
    public enum BatchOutputFormat
    {
        SameAsSource,
        Png,
        Jpeg,
        Bmp,
        Tiff,
        WebP,
    }

    public sealed record BatchOutputFormatItem(BatchOutputFormat Value, string Name);
}
