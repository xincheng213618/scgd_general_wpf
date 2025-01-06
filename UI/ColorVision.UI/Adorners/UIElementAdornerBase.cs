#pragma warning disable CA1512 

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ColorVision.Adorners
{
    public class UIElementAdornerBase : Adorner
    {
        protected Grid AdornerVisual { get; private set; } = new Grid();
        protected AdornerLayer AdornerLayer { get; private set; }

        protected UIElementAdornerBase(UIElement adornedElement) : base(adornedElement)
        {
            AdornerLayer = AdornerLayer.GetAdornerLayer(adornedElement);
            AdornerLayer?.Add(this);
        }

        public void Detach()
        {
            AdornerLayer?.Remove(this);
        }


        protected override int VisualChildrenCount
        {
            get { return 1; }
        }

        protected override Visual GetVisualChild(int index)
        {
            if (VisualChildrenCount <= index)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return AdornerVisual;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            AdornerVisual.Measure(constraint);
            return base.MeasureOverride(constraint);
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            AdornerVisual.Arrange(new Rect(finalSize));
            return base.ArrangeOverride(finalSize);
        }
    }
}
