using System.Windows.Media;

namespace ColorVision.UI.Draw
{
    public interface IDrawingVisualDatum
    {
        public abstract DrawBaseAttribute BaseAttribute { get; }

        public abstract Pen Pen { get; set; }
        public abstract void Render();
    }
}
