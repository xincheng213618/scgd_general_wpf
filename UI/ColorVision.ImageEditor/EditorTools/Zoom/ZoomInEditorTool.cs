using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomInEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "ZoomUniform";

        public int Order { get; set; } = 1;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImagezoom_in");

        public ICommand? Command { get; set; } = new RelayCommand((o) =>
        {
            EditorContext.ZoomboxSub.Zoom(1.25);
        });
    }


}
