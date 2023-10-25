using System.Windows.Controls;
using System.Windows;
namespace ColorVision.Draw.Converter
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
            if (item is DrawingVisualCircle)
                return DrawingVisualCircleTemplate;
            if (item is DrawingVisualCircleWord)
                return DrawingVisualCircleWordTemplate;
            if (item is DrawingVisualRectangle)
                return DrawingVisualRectangleTemplate;
            if (item is DrawingVisualRectangleWord)
                return DrawingVisualRectangleWordTemplate;
            return base.SelectTemplate(item, container);

        }
    }
}
