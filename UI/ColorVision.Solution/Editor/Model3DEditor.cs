using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.EditorTools.ThreeD;
using ColorVision.Solution.Workspace;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Editor
{
    [EditorForExtension(".obj|.stl", "3D模型查看器", isDefault: true, editorId: "colorvision.model3d", priority: 100, isVisibleInOpenWith: false)]
    public class Model3DEditor : EditorBase
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
                    var control = new ModelViewer3DControl();
                    control.SetInitialFile(filePath);
                    return control;
                },
                control => control.DisposeViewer());
        }
    }
}
