using System.Windows.Media;

namespace ColorVision
{
    public interface IDrawingVisualDatum
    {
        public abstract DrawBaseAttribute GetAttribute();

        public abstract Pen Pen { get; set; }
        public abstract void Render();


    }



}
