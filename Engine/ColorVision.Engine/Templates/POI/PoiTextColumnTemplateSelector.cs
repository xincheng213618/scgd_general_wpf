using ColorVision.ImageEditor.Draw;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI
{
    public class PoiTextColumnTemplateSelector : DataTemplateSelector
    {
        public DataTemplate CircleTextTemplate { get; set; }
        public DataTemplate RectangleTextTemplate { get; set; }
        public DataTemplate EmptyTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is DVCircleText)
                return CircleTextTemplate;
            if (item is DVRectangleText)
                return RectangleTextTemplate;
            return EmptyTemplate ?? base.SelectTemplate(item, container);
        }
    }
}
