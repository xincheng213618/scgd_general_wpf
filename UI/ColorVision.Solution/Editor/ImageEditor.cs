using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Windows;


namespace ColorVision.Solution.Editor
{
    // 声明支持的图片扩展名，设置为默认编辑器
    [EditorForExtension(".jpg|.png|.jpeg|.tif|.bmp|.tiff|.cvraw|.cvcie", "图片编辑器", isDefault: true, resourceKey: "Sol_Editor_Image", editorId: "colorvision.image", priority: 100)]
    public class ImageEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            EditorDocumentService.Open(
                filePath,
                GetType(),
                Path.GetFileName(filePath),
                () =>
                {
                    var imageView = new ImageView();
                    imageView.OpenImage(filePath);
                    return imageView;
                },
                imageView =>
                {
                    imageView.Clear();
                    imageView.Dispose();
                });
        }
    }

}
