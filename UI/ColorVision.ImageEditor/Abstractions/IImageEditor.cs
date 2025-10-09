namespace ColorVision.ImageEditor
{
    /// <summary>
    /// 图像组件接口 - 定义可在 ImageView 上执行的操作
    /// </summary>
    public interface IImageComponent
    {
        void Execute(ImageView imageView);
    }

    /// <summary>
    /// 图像打开接口 - 定义图像文件的打开方式
    /// </summary>
    public interface IImageOpen
    {
        void OpenImage(ImageView imageView, string? filePath);
    }
}
