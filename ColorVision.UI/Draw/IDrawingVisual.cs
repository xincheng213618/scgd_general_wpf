using System.Windows.Media;

namespace ColorVision.UI.Draw
{
    public interface IDrawingVisual
    {
        public abstract DrawBaseAttribute BaseAttribute { get; }
        public bool AutoAttributeChanged { get; set; }

        public Pen Pen { get; set; }

        public abstract void Render();

    }



}
