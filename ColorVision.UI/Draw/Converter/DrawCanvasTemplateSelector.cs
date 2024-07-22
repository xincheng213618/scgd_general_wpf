using System.Windows.Controls;
using System.Windows;
namespace ColorVision.UI.Draw.Converter
{

    public class DrawCanvasTemplateSelector : DataTemplateSelector
    {
        public DrawCanvasTemplateSelector()
        {
        }

        public DataTemplate DrawingVisualCircleTemplate { get; set; }
        public DataTemplate DrawingVisualRectangleTemplate { get; set; }
        public DataTemplate DrawingVisualCircleWordTemplate { get; set; }
        public DataTemplate DrawingVisualRectangleWordTemplate { get; set; }



        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is DVCircle)
                return DrawingVisualCircleTemplate;
            if (item is DVCircleText)
                return DrawingVisualCircleWordTemplate;
            if (item is DVRectangle)
                return DrawingVisualRectangleTemplate;
            if (item is DVRectangleText)
                return DrawingVisualRectangleWordTemplate;
            return base.SelectTemplate(item, container);

        }
    }
}
