using HelixToolkit.Wpf;
using ScottPlot.Styles;
using System;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
                    Position = new Point3D(newWidth / 2, -newHeight * 1, newHeight / 2), // 相机位置
                    LookDirection = new Vector3D(0, newHeight * 2, -newHeight / 2), // 看向模型中心
                    UpDirection = new Vector3D(0, 0, 1), // Z 轴向上
                    FieldOfView = 60,

                },
                ShowFrameRate = true,
                ZoomExtentsWhenLoaded = true,
            };

            viewport.Children.Add(new DefaultLights());
            // 添加 GridLinesVisual3D
            GridLinesVisual3D gridLinesVisual3D = new GridLinesVisual3D
            {
                Length = newWidth, // 网格线的长度
                Width = newHeight, // 网格线的宽度
                Center = new Point3D(newWidth / 2, newHeight / 2, 0) // 将网格线的中心移动到图像的中心
            };
            viewport.Children.Add(gridLinesVisual3D);

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

        int FindClosestFactor(int value, int[] factors)
        {
            int closest = factors[0];
            foreach (int factor in factors)
            {
                if (Math.Abs(value - factor) < Math.Abs(value - closest))
                {
                    closest = factor;
                }
            }
            return closest;
        }
        public void GenGrayPixels()
        {
            scaleFactor = 4; // 降低分辨率的比例因子，例如 4 表示将分辨率降低到原来的 1/4
            int targetPixels = 512 * 512; // 目标像素数

            int originalWidth = colorBitmap.PixelWidth;
            int originalHeight = colorBitmap.PixelHeight;

            // 计算初始比例因子
            double initialScaleFactor = Math.Sqrt((double)originalWidth * originalHeight / targetPixels);

            // 确保比例因子是 1、2、4、8 等倍数
            int[] allowedFactors = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048 };
            scaleFactor = FindClosestFactor((int)Math.Round(initialScaleFactor), allowedFactors);

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
                    int flippedY = newHeight - 1 - y;

                    double z1 = grayPixels[y * newWidth + x] / 255.0 * heightScale;
                    double z2 = grayPixels[y * newWidth + (x + 1)] / 255.0 * heightScale;
                    double z3 = grayPixels[(y + 1) * newWidth + x] / 255.0 * heightScale;
                    double z4 = grayPixels[(y + 1) * newWidth + (x + 1)] / 255.0 * heightScale;

                    // 创建两个三角形来表示一个网格单元
                    meshBuilder.AddTriangle(new Point3D(x, flippedY, z1), new Point3D(x + 1, flippedY, z2), new Point3D(x, flippedY + 1, z3));
                    meshBuilder.AddTriangle(new Point3D(x + 1, flippedY, z2), new Point3D(x + 1, flippedY + 1, z4), new Point3D(x, flippedY + 1, z3));
                }
            }

            var mesh = meshBuilder.ToMesh();
            mesh.TextureCoordinates = null;

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
            heightScale *= 1.1; // 例如，每次点击增加 10.0
            GenOpenGLAsync(heightScale); // 异步调用
        }
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // 更改 heightScale 的值
            heightScale *= 0.9; // 例如，每次点击增加 10.0
            GenOpenGLAsync(heightScale); // 异步调用
        }

        private void L_Click(object sender, RoutedEventArgs e)
        {
            var Position = viewport.Camera.Position;
            viewport.Camera.Position = new Point3D(Position.X - 10, Position.Y, Position.Z);

        }

        private void R_Click(object sender, RoutedEventArgs e)
        {
            var Position = viewport.Camera.Position;
            viewport.Camera.Position = new Point3D(Position.X + 10, Position.Y, Position.Z);
        }

        private void T_Click(object sender, RoutedEventArgs e)
        {
            var Position = viewport.Camera.Position;
            viewport.Camera.Position = new Point3D(Position.X , Position.Y +10, Position.Z);
        }

        private void B_Click(object sender, RoutedEventArgs e)
        {
            var Position = viewport.Camera.Position;
            viewport.Camera.Position = new Point3D(Position.X, Position.Y -10, Position.Z);
        }

        private void D_Click(object sender, RoutedEventArgs e)
        {
            var Position = viewport.Camera.LookDirection;
            viewport.Camera.LookDirection = new Vector3D(Position.X, Position.Y - 10, Position.Z);
        }

        private void F_Click(object sender, RoutedEventArgs e)
        {
            var Position = viewport.Camera.LookDirection;
            viewport.Camera.LookDirection = new Vector3D(Position.X, Position.Y + 10, Position.Z);
        }

        private void A_Click(object sender, RoutedEventArgs e)
        {
            var Position = viewport.Camera.LookDirection;
            viewport.Camera.LookDirection = new Vector3D(Position.X, Position.Y, Position.Z +10);
        }

        private void C_Click(object sender, RoutedEventArgs e)
        {
            var Position = viewport.Camera.LookDirection;
            viewport.Camera.LookDirection = new Vector3D(Position.X, Position.Y, Position.Z- 10);
        }
    }
}
