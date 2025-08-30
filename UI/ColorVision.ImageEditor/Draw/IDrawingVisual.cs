using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public interface IDrawingVisual
    {
        public abstract BaseProperties BaseAttribute { get; }

        public Pen Pen { get; set; }

        public abstract void Render();

    }



}
