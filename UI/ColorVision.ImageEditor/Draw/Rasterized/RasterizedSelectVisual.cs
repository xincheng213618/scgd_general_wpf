using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw.Rasterized
{

    public class RasterizedPropertie:BaseProperties
    {

    }

    public class RasterizedSelectVisual : DrawingVisualBase<RasterizedPropertie>, IDrawingVisual,ISelectVisual
    {
        public Pen Pen { get; set; } = new Pen();

        public BitmapSource Image { get; set; }
        public Rect Rect { get; set; }

        public RasterizedSelectVisual(BitmapSource image, Rect rect)
        {
            Attribute = new RasterizedPropertie();
            Image = image;
            Rect = rect;
            RenderImage();
        }


        private void RenderImage()
        {
            using (var dc = this.RenderOpen())
            {
                dc.DrawImage(Image, Rect);
            }
        }

        public override Rect GetRect() => Rect;

        public override void SetRect(Rect rect)
        {
            // 可选：支持拖动等操作
            Rect = rect;
            RenderImage();
        }
    }
}
