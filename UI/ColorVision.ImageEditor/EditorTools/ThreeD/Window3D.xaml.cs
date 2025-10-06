using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.UI;
using HelixToolkit.Wpf;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ColorVision.ImageEditor
{
    public class Window3DConfig : ViewModelBase, IConfig
    {
        public static Window3DConfig Instance => ConfigService.Instance.GetRequiredService<Window3DConfig>();

        public int TargetPixelsX { get => _TargetPixelsX; set { _TargetPixelsX = 512;OnPropertyChanged(); } }
        private int _TargetPixelsX = 512;

        public int TargetPixelsY { get => _TargetPixelsY; set { _TargetPixelsY = 512; OnPropertyChanged(); } }
        private int _TargetPixelsY = 512;
    }



    public partial class Window3D : Window
    {
        WriteableBitmap colorBitmap { get; set; }
        HelixViewport3D viewport;
        ModelVisual3D modelVisual;
        byte[] grayPixels;
        int newWidth;
        int newHeight;
        double heightScale = 100.0; // 初始化 heightScale

        public static Window3DConfig Config => Window3DConfig.Instance;

        public Window3D(WriteableBitmap writeableBitmap)
        {
            this.colorBitmap = writeableBitmap;
            InitializeComponent();
           
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            HImage hImage = colorBitmap.ToHImage();
            // 调用 C++ 函数并接收输出数据
            IntPtr rowGrayPixelsPtr;
            int length;
            int scaleFactor = 2;  // 设置缩放因子

            int ret = OpenCVMediaHelper.M_ConvertImage(hImage, out rowGrayPixelsPtr, out length, scaleFactor, Config.TargetPixelsX,Config.TargetPixelsY);
            if (ret == 0)
            {
                // 将返回的指针转换为字节数组
                grayPixels = new byte[length];
                Marshal.Copy(rowGrayPixelsPtr, grayPixels, 0, length);
                // 释放指针
                Marshal.FreeHGlobal(rowGrayPixelsPtr);
            }

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
                ZoomExtentsWhenLoaded =true,
                
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
            viewport.CameraController.AddRotateForce(0,4.5);
            this.PreviewKeyDown += Window3D_PreviewKeyDown;
        }

        private void Window3D_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Add)
            {
                heightScale *= 1.1; // 例如，每次点击增加 10.0
                GenOpenGLAsync(heightScale); // 异步调用
            }
            if(e.Key == Key.Subtract)
            {
                heightScale *= 0.9; // 例如，每次点击增加 10.0
                GenOpenGLAsync(heightScale); // 异步调用
            }
            if (e.Key == System.Windows.Input.Key.L)
            {
                viewport.Camera.Position = new Point3D(Position.X - 10, Position.Y, Position.Z);
            }
            if (e.Key == Key.A)
            {
                viewport.Camera.LookDirection = new Vector3D(Position.X, Position.Y, Position.Z + 10);
            }
            if (e.Key == Key.R)
            {
                viewport.Camera.Position = new Point3D(Position.X + 10, Position.Y, Position.Z);
            }
            if(e.Key == Key.B)
            {
                viewport.Camera.Position = new Point3D(Position.X, Position.Y - 10, Position.Z);
            }

        }

        static int FindClosestFactor(int value, int[] factors)
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
        public void GenGrayPixels(int scaleFactor =-1)
        {
            if (scaleFactor == -1)
            {
                int targetPixels = Config.TargetPixelsX * Config.TargetPixelsY; // 目标像素数

                int originalWidth = colorBitmap.PixelWidth;
                int originalHeight = colorBitmap.PixelHeight;

                // 计算初始比例因子
                double initialScaleFactor = Math.Sqrt((double)originalWidth * originalHeight / targetPixels);

                // 确保比例因子是 1、2、4、8 等倍数
                int[] allowedFactors = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048 };
                scaleFactor = FindClosestFactor((int)Math.Round(initialScaleFactor), allowedFactors);
            }
            newWidth = colorBitmap.PixelWidth / scaleFactor;
            newHeight = colorBitmap.PixelHeight / scaleFactor;
            return;
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
        public Vector3D Position => viewport.Camera.LookDirection;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            heightScale *= 1.1; // 例如，每次点击增加 10.0
            GenOpenGLAsync(heightScale); // 异步调用
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // 更改 heightScale 的值
            heightScale *= 0.9; // 例如，每次点击增加 10.0
            GenOpenGLAsync(heightScale); // 异步调用
        }

        private void L_Click(object sender, RoutedEventArgs e)
        {
            viewport.Camera.Position = new Point3D(Position.X - 10, Position.Y, Position.Z);

        }

        private void R_Click(object sender, RoutedEventArgs e)
        {
            viewport.Camera.Position = new Point3D(Position.X + 10, Position.Y, Position.Z);
        }

        private void T_Click(object sender, RoutedEventArgs e)
        {
            viewport.Camera.Position = new Point3D(Position.X , Position.Y +10, Position.Z);
        }

        private void B_Click(object sender, RoutedEventArgs e)
        {
            viewport.Camera.Position = new Point3D(Position.X, Position.Y -10, Position.Z);
        }

        private void D_Click(object sender, RoutedEventArgs e)
        {
            viewport.Camera.LookDirection = new Vector3D(Position.X, Position.Y - 10, Position.Z);
        }

        private void F_Click(object sender, RoutedEventArgs e)
        {
            viewport.Camera.LookDirection = new Vector3D(Position.X, Position.Y + 10, Position.Z);
        }


        private void A_Click(object sender, RoutedEventArgs e)
        {
            viewport.Camera.LookDirection = new Vector3D(Position.X, Position.Y, Position.Z +10);
        }

        private void C_Click(object sender, RoutedEventArgs e)
        {
            viewport.Camera.LookDirection = new Vector3D(Position.X, Position.Y, Position.Z- 10);
        }
    }
}
