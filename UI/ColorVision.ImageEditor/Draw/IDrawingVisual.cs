using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public interface IDrawingVisual
    {
        public abstract BaseProperties BaseAttribute { get; }
        public bool AutoAttributeChanged { get; set; }

        public Pen Pen { get; set; }

        public abstract void Render();

    }



}
