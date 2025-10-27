namespace ColorVision.ImageEditor.Abstractions
{
    public interface IImageComponent
    {
        void Execute(ImageView imageView);
    }

    public interface IImageOpen
    {
        void OpenImage(EditorContext context, string? filePath);
    }
}
