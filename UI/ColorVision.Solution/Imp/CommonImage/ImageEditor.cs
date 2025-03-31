using ColorVision.ImageEditor;
using System.Windows.Controls;


namespace ColorVision.Solution.Imp.CommonImage
{
    public class ImageEditor : IEditorBase
    {
        public override string Extension => ".jpg|.png|.jpeg|.tif|.bmp|.tiff|.cvraw|.cvcie";
        public override Control? Open(string FilePath)
        {
            ImageView imageView = ImageView.GetInstance();
            imageView.OpenImage(FilePath);
            return imageView;
        }
    }

}
