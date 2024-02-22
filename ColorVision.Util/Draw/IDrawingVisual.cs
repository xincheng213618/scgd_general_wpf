using System.Windows.Media;

namespace ColorVision.Draw
{
    public interface IDrawingVisual
    {
        public abstract DrawBaseAttribute BaseAttribute { get; }
        public bool AutoAttributeChanged { get; set; }

        public Pen Pen { get; set; }

        public abstract void Render();

    }



}
