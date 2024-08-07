using HelixToolkit.Wpf;
using ScottPlot.Styles;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ColorVision.Engine.Media
{
    public partial class Window3D : Window
    {
        WriteableBitmap colorBitmap { get; set; }
        HelixViewport3D viewport;
        ModelVisual3D modelVisual;
        byte[] grayPixels;
        int scaleFactor;
        int newWidth;
        int newHeight;
        double heightScale = 100.0; // 初始化 heightScale

        public Window3D(WriteableBitmap writeableBitmap)
        {
            this.colorBitmap = writeableBitmap;
            InitializeComponent();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            GenGrayPixels();

            viewport = new HelixViewport3D
            {
                Camera = new PerspectiveCamera
                {
                    Position = new Point3D(newWidth / 2, newHeight * 1.5, newHeight / 2), // 相机位置
                    LookDirection = new Vector3D(0, -newHeight * 1.5, -newHeight / 2), // 看向模型中心
                    UpDirection = new Vector3D(0, 0, 1), // Z 轴向上
                    FieldOfView = 60,
                }
            };

            viewport.Children.Add(new DefaultLights());

            // 添加坐标轴
            var coordinateSystem = new CoordinateSystemVisual3D
            {
                ArrowLengths = newWidth / 10 // 调整坐标轴大小，使其与点云比例相当
            };
            viewport.Children.Add(coordinateSystem);

            ContentGrid.Children.Add(viewport);
            await Task.Delay(10);

            GenOpenGLAsync(heightScale); // 异步调用
        }

        public void GenGrayPixels()
        {
            scaleFactor = 4; // 降低分辨率的比例因子，例如 4 表示将分辨率降低到原来的 1/4
            newWidth = colorBitmap.PixelWidth / scaleFactor;
            newHeight = colorBitmap.PixelHeight / scaleFactor;

            int stride = colorBitmap.PixelWidth * (colorBitmap.Format.BitsPerPixel / 8);
            byte[] originalPixels = new byte[colorBitmap.PixelHeight * stride];
            colorBitmap.CopyPixels(originalPixels, stride, 0);

            var PixelWidth = colorBitmap.PixelWidth;
            grayPixels = new byte[newWidth * newHeight];

            if (colorBitmap.Format == PixelFormats.Bgr24 || colorBitmap.Format == PixelFormats.Bgr32 || colorBitmap.Format == PixelFormats.Bgra32)
            {
                unsafe
                {
                    fixed (byte* pOriginalPixels = originalPixels)
                    fixed (byte* pGrayPixels = grayPixels)
                    {
                        byte* localOriginalPixels = pOriginalPixels;
                        byte* localGrayPixels = pGrayPixels;

                        Parallel.For(0, newHeight, y =>
                        {
                            byte* rowGrayPixels = localGrayPixels + y * newWidth;
                            for (int x = 0; x < newWidth; x++)
                            {
                                int oldX = x * scaleFactor;
                                int oldY = y * scaleFactor;
                                int oldIndex = (oldY * PixelWidth + oldX) * 3;

                                byte* pixel = localOriginalPixels + oldIndex;
                                byte b = pixel[0];
                                byte g = pixel[1];
                                byte r = pixel[2];

                                // 使用加权平均值计算灰度值
                                byte gray = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                                rowGrayPixels[x] = gray;
                            }
                        });
                    }
                }
            }
            else if (colorBitmap.Format == PixelFormats.Rgb24)
            {
                unsafe
                {
                    fixed (byte* pOriginalPixels = originalPixels)
                    fixed (byte* pGrayPixels = grayPixels)
                    {
                        byte* localOriginalPixels = pOriginalPixels;
                        byte* localGrayPixels = pGrayPixels;

                        Parallel.For(0, newHeight, y =>
                        {
                            byte* rowGrayPixels = localGrayPixels + y * newWidth;
                            for (int x = 0; x < newWidth; x++)
                            {
                                int oldX = x * scaleFactor;
                                int oldY = y * scaleFactor;
                                int oldIndex = (oldY * PixelWidth + oldX) * 3;

                                byte* pixel = localOriginalPixels + oldIndex;
                                byte b = pixel[2];
                                byte g = pixel[1];
                                byte r = pixel[0];
                                // 使用加权平均值计算灰度值
                                byte gray = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                                rowGrayPixels[x] = gray;
                            }
                        });
                    }
                }

            }
            else if (colorBitmap.Format == PixelFormats.Gray8)
            {
                unsafe
                {
                    fixed (byte* pOriginalPixels = originalPixels)
                    fixed (byte* pGrayPixels = grayPixels)
                    {
                        byte* localOriginalPixels = pOriginalPixels;
                        byte* localGrayPixels = pGrayPixels;

                        Parallel.For(0, newHeight, y =>
                        {
                            byte* rowGrayPixels = localGrayPixels + y * newWidth;
                            for (int x = 0; x < newWidth; x++)
                            {
                                int oldX = x * scaleFactor;
                                int oldY = y * scaleFactor;
                                int oldIndex = (oldY * PixelWidth + oldX) * 3;

                                byte* pixel = localOriginalPixels + oldIndex;
                                byte gray = pixel[0];
                                rowGrayPixels[x] = gray;
                            }
                        });
                    }
                }
            }


        }

        public void GenOpenGLAsync(double heightScale)
        {
            var meshBuilder = new MeshBuilder(false, false);
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

                }
            }

            var mesh = meshBuilder.ToMesh();
            mesh.TextureCoordinates = null;

            var gradientBrush = new LinearGradientBrush();
            gradientBrush.GradientStops.Add(new GradientStop(Colors.Red, 0));
            gradientBrush.GradientStops.Add(new GradientStop(Colors.Black, 1));

            var material = new DiffuseMaterial { Brush = Brushes.White };
            var geometryModel = new GeometryModel3D(mesh, material);

            // 更新 modelVisual 的内容
            if (modelVisual == null)
            {
                modelVisual = new ModelVisual3D { Content = geometryModel };
                viewport.Children.Add(modelVisual);
            }
            else
            {
                modelVisual.Content = geometryModel;
            }
        }





        private Color GetColorForHeight(double height)
        {
            // 根据高度值返回颜色
            if (height < 0.3) return Colors.Blue;
            if (height < 0.6) return Colors.Green;
            if (height < 0.9) return Colors.Yellow;
            return Colors.Red;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            // 更改 heightScale 的值
            heightScale += 10.0; // 例如，每次点击增加 10.0
            GenOpenGLAsync(heightScale); // 异步调用
        }
    }
}
