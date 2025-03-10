using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class EditBase : Stroke
    {
        public EditBase(StylusPointCollection points) : base(points)
        {
            StylusPoints = points.Clone();
        }
    }

    public class EditBase<T> : Stroke where T : BaseProperties, new()
    {
        public T Attribute { get; set; }
        public EditBase(StylusPointCollection points) : base(points)
        {
            StylusPoints = points.Clone();
        }

    }
    public class CircleEdit : EditBase<CircleProperties>
    {
        public CircleEdit(StylusPointCollection stylusPoints) : base(new StylusPointCollection())
        {
        }
        protected override void DrawCore(DrawingContext drawingContext, DrawingAttributes drawingAttributes)
        {
            base.DrawCore(drawingContext, drawingAttributes);
        }
    }



}
