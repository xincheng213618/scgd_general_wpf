using System.Windows.Controls.Primitives;

namespace ColorVision.UI.Extension
{
    public static class UniformGridExtension
    {
        public static void AutoUpdateLayout(this UniformGrid uniformGrid, double itemWidth =120)
        {
            double actualWidth = uniformGrid.ActualWidth;
            int childrenCount = uniformGrid.Children.Count;

            // 确保实际宽度和子元素数量非负
            if (actualWidth < 0)
            {
                actualWidth = 0;
            }

            if (childrenCount < 0)
            {
                childrenCount = 0;
            }
            // 计算列数，确保至少为1
            int columns = actualWidth > 0 ? (int)(actualWidth / itemWidth) : 1;
            uniformGrid.Columns = Math.Max(columns, 1);
            // 计算行数
            uniformGrid.Rows = (int)Math.Ceiling(childrenCount / (double)uniformGrid.Columns);
        }
    }
}
