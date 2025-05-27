using ColorVision.ImageEditor;
using System.Windows.Controls;


namespace ColorVision.Solution.Editor
{
    // 声明支持的图片扩展名，设置为默认编辑器
    [EditorForExtension(".jpg|.png|.jpeg|.tif|.bmp|.tiff|.cvraw|.cvcie", "图片编辑器", isDefault: true)]
    public class ImageEditor : EditorBase
    {
        public override Control? Open(string filePath)
        {
            ImageView imageView = new ImageView();
            imageView.OpenImage(filePath);
            return imageView;
        }
    }

}
