using System.Windows.Controls;
using System.Windows;

namespace ColorVision.Converter
{

    public class DrawCanvasTemplateSelector : DataTemplateSelector
    {
        public DrawCanvasTemplateSelector()
        {


        }


        public DataTemplate DrawingVisualCircleTemplate { get; set; }
        public DataTemplate DrawingVisualRectangleTemplate { get; set; }



        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {


            if (item is DrawingVisualCircle )
            {
                return DrawingVisualCircleTemplate;
            }
            else if (item is DrawingVisualRectangle)
            {
                return DrawingVisualRectangleTemplate;
            }
            else
            {
                return base.SelectTemplate(item, container);
            }
        }
    }
}
