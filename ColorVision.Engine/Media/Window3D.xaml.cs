using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace ColorVision.Engine.Media
{
    /// <summary>
    /// Window3D.xaml 的交互逻辑
    /// </summary>
    public partial class Window3D : Window
    {
        WriteableBitmap colorBitmap { get; set; }
        public Window3D(WriteableBitmap writeableBitmap)
        {
            this.colorBitmap = writeableBitmap;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

            int scaleFactor = 8; // 降低分辨率的比例因子，例如 4 表示将分辨率降低到原来的 1/4
            int newWidth = colorBitmap.PixelWidth / scaleFactor;
            int newHeight = colorBitmap.PixelHeight / scaleFactor;

            int stride = colorBitmap.PixelWidth * 3;
            byte[] originalPixels = new byte[colorBitmap.PixelHeight * stride];
            colorBitmap.CopyPixels(originalPixels, stride, 0);

            byte[] grayPixels = new byte[newWidth * newHeight];
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    int oldX = x * scaleFactor;
                    int oldY = y * scaleFactor;
                    int oldIndex = (oldY * colorBitmap.PixelWidth + oldX) * 3;

                    byte b = originalPixels[oldIndex];
                    byte g = originalPixels[oldIndex + 1];
                    byte r = originalPixels[oldIndex + 2];

                    // 使用加权平均值计算灰度值
                    byte gray = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                    grayPixels[y * newWidth + x] = gray;
                }
            }

            var meshBuilder = new MeshBuilder(false, false);
            double heightScale = 100.0; // 调整此值以增强高度效果

            var colors = new Color[newWidth * newHeight];

            for (int x = 0; x < newWidth - 1; x++)
            {
                for (int y = 0; y < newHeight - 1; y++)
                {
                    double z1 = grayPixels[y * newWidth + x] / 255.0 * heightScale;
                    double z2 = grayPixels[y * newWidth + (x + 1)] / 255.0 * heightScale;
                    double z3 = grayPixels[(y + 1) * newWidth + x] / 255.0 * heightScale;
                    double z4 = grayPixels[(y + 1) * newWidth + (x + 1)] / 255.0 * heightScale;

                    // 创建两个三角形来表示一个网格单元
                    meshBuilder.AddTriangle(new Point3D(x, y, z1), new Point3D(x + 1, y, z2), new Point3D(x, y + 1, z3));
                    meshBuilder.AddTriangle(new Point3D(x + 1, y, z2), new Point3D(x + 1, y + 1, z4), new Point3D(x, y + 1, z3));

                    colors[y * newWidth + x] = GetColorForHeight(z1 / heightScale);
                }
            }

            var mesh = meshBuilder.ToMesh();

            // 使用 VertexColors 为不同高度染色
            mesh.TextureCoordinates = null;

            var material = new DiffuseMaterial { Brush = Brushes.White };
            var geometryModel = new GeometryModel3D(mesh, material);

            var modelVisual = new ModelVisual3D { Content = geometryModel };

            // 设置 Viewport3D
            var viewport = new HelixViewport3D
            {
                Camera = new PerspectiveCamera
                {
                    Position = new Point3D(newWidth / 2, -newHeight * 1.5, newHeight / 2), // 相机位置
                    LookDirection = new Vector3D(0, newHeight * 1.5, -newHeight / 2), // 看向模型中心
                    UpDirection = new Vector3D(0, 0, 1), // Z 轴向上
                    FieldOfView = 60,
                }
            };

            viewport.Children.Add(new DefaultLights());
            viewport.Children.Add(modelVisual);

            // 添加坐标轴
            var coordinateSystem = new CoordinateSystemVisual3D
            {
                ArrowLengths = newWidth/10 // 调整坐标轴大小，使其与点云比例相当
            };
            viewport.Children.Add(coordinateSystem);

            // 将 Viewport3D 添加到窗口
            this.Content = viewport;
        }

        private Color GetColorForHeight(double height)
        {
            // 根据高度值返回颜色
            if (height < 0.3) return Colors.Blue;
            if (height < 0.6) return Colors.Green;
            if (height < 0.9) return Colors.Yellow;
            return Colors.Red;
        }
    }
}
