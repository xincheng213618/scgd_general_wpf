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

    public class RasterizedSelectVisual : DrawingVisualBase<RasterizedPropertie>, ISelectVisual
    {
        public BitmapSource Image { get; }
        public Rect Rect { get; }

        public RasterizedSelectVisual(BitmapSource image, Rect rect)
        {
            Image = image;
            Rect = rect;
            RenderImage();
        }

        private void RenderImage()
        {
            using (var dc = this.RenderOpen())
            {
                dc.DrawImage(Image, Rect);
                // 你可以在此绘制边框等辅助元素
            }
        }

        public Rect GetRect() => Rect;

        public void SetRect(Rect rect)
        {
            // 可选：支持拖动等操作
            // Rect = rect;
            // RenderImage();
        }
    }
}
