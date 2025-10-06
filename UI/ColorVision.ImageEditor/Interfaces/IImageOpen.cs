#pragma warning disable CS8625
namespace ColorVision.ImageEditor
{
    public interface IImageOpen
    {
        public void OpenImage(ImageView imageView, string? filePath);
    }

}
