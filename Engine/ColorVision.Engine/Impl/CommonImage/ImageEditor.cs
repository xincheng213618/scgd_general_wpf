using ColorVision.Engine.Templates.Flow;
using ColorVision.ImageEditor;
using ColorVision.Solution;
using ColorVision.UI;
using System;
using System.Windows.Controls;


namespace ColorVision.Engine.Impl.CommonImage
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
