using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor
{
    public interface IDrawingVisual
    {
        public abstract BaseProperties BaseAttribute { get; }

        public Pen Pen { get; set; }

        public abstract void Render();

    }
    public class IDrawingVisualDVContextMenu : IDVContextMenu
    {
        public Type ContextType => typeof(IDrawingVisual);

        public IEnumerable<MenuItem> GetContextMenuItems(ImageViewModel imageViewModel, object obj)
        {
            List<MenuItem> MenuItems = new List<MenuItem>();
            if (obj is IDrawingVisual drawingVisual)
            {

            }
            return MenuItems;
        }
    }


}
