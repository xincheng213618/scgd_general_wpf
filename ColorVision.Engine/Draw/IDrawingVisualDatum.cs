using System.Windows.Media;

namespace ColorVision.Engine.Draw
{
    public interface IDrawingVisualDatum
    {
        public abstract BaseProperties BaseAttribute { get; }

        public abstract Pen Pen { get; set; }
        public abstract void Render();
    }
}
