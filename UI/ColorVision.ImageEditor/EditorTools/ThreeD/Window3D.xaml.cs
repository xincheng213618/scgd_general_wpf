using ColorVision.Common.MVVM;
using ColorVision.UI;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ColorVision.ImageEditor
{
    public class Window3DConfig : ViewModelBase, IConfig
    {
        public static Window3DConfig Instance => ConfigService.Instance.GetRequiredService<Window3DConfig>();

        public int TargetPixelsX { get => _TargetPixelsX; set { _TargetPixelsX = value; OnPropertyChanged(); } }
        private int _TargetPixelsX = 512;

        public int TargetPixelsY { get => _TargetPixelsY; set { _TargetPixelsY = value; OnPropertyChanged(); } }
        private int _TargetPixelsY = 512;

        public string SelectedColormap { get => _SelectedColormap; set { _SelectedColormap = value; OnPropertyChanged(); } }
        private string _SelectedColormap = "jet";
    }

    public record ColormapInfo(string Name, BitmapImage? ImageSource, byte[]? Lut);

    public partial class Window3D : Window
    {
        private readonly WriteableBitmap colorBitmap;
        private HelixViewport3D? viewport;
        private ModelVisual3D? modelVisual;
        private byte[]? grayPixels;
        private int newWidth;
        private int newHeight;
        private double heightScale = 100.0;
        private DiffuseMaterial? colormapMaterial;
        private ColormapInfo? currentColormap;

        private static readonly string[] ColormapNames =
        {
            "jet", "viridis", "plasma", "inferno", "magma", "cividis",
            "turbo", "hot", "cool", "spring", "summer", "autumn", "winter",
            "bone", "pink", "ocean", "rainbow", "hsv", "deepgreen",
            "parula", "twilight", "twilight_shifted", "mkpj1", "mkpj2"
        };

        private static readonly Lazy<List<ColormapInfo>> AllColormaps = new(() =>
        {
            var list = new List<ColormapInfo>();
            foreach (var name in ColormapNames)
            {
                try
                {
                    var uri = new Uri($"pack://application:,,,/ColorVision.ImageEditor;component/Assets/Colormap/colorscale_{name}.jpg");
                    var image = new BitmapImage(uri);
                    image.Freeze();
                    var lut = ExtractLutFromColormap(image);
                    list.Add(new ColormapInfo(name, image, lut));
                }
                catch { }
            }
            return list;
        });

        private static byte[] ExtractLutFromColormap(BitmapSource image)
        {
            var bgr = new FormatConvertedBitmap(image, PixelFormats.Bgr24, null, 0);
            int w = bgr.PixelWidth;
            int h = bgr.PixelHeight;
            int stride = w * 3;
            byte[] pixels = new byte[h * stride];
            bgr.CopyPixels(pixels, stride, 0);

            byte[] lut = new byte[256 * 3];
            int midX = w / 2;
            for (int i = 0; i < 256; i++)
            {
                int row = (int)((1.0 - i / 255.0) * (h - 1));
                row = Math.Clamp(row, 0, h - 1);
                int offset = row * stride + midX * 3;
                lut[i * 3] = pixels[offset];
                lut[i * 3 + 1] = pixels[offset + 1];
                lut[i * 3 + 2] = pixels[offset + 2];
            }
            return lut;
        }

        public static Window3DConfig Config => Window3DConfig.Instance;

        public Window3D(WriteableBitmap writeableBitmap)
        {
            colorBitmap = writeableBitmap;
            InitializeComponent();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            (grayPixels, newWidth, newHeight) = ConvertBitmapToGray(
                colorBitmap, Config.TargetPixelsX, Config.TargetPixelsY);

            if (grayPixels == null || grayPixels.Length == 0) return;

            // Load colormaps and populate ComboBox
            var colormaps = AllColormaps.Value;
            ComboBoxColormap.ItemsSource = colormaps;

            int selectedIndex = colormaps.FindIndex(c => c.Name == Config.SelectedColormap);
            if (selectedIndex < 0) selectedIndex = 0;
            ComboBoxColormap.SelectedIndex = selectedIndex;

            // Init settings popup values
            TxtTargetX.Text = Config.TargetPixelsX.ToString();
            TxtTargetY.Text = Config.TargetPixelsY.ToString();

            viewport = new HelixViewport3D
            {
                Camera = new PerspectiveCamera
                {
                    Position = new Point3D(newWidth / 2, -newHeight, newHeight / 2),
                    LookDirection = new Vector3D(0, newHeight * 2, -newHeight / 2),
                    UpDirection = new Vector3D(0, 0, 1),
                    FieldOfView = 60,
                },
                ShowFrameRate = true,
                ZoomExtentsWhenLoaded = true,
            };

            viewport.Children.Add(new DefaultLights());
            viewport.Children.Add(new GridLinesVisual3D
            {
                Length = newWidth,
                Width = newHeight,
                Center = new Point3D(newWidth / 2, newHeight / 2, 0)
            });
            viewport.Children.Add(new CoordinateSystemVisual3D
            {
                ArrowLengths = newWidth / 10
            });
            ContentGrid.Children.Add(viewport);

            await UpdateMeshAsync();
            viewport.CameraController.AddRotateForce(0, 4.5);
            PreviewKeyDown += Window3D_PreviewKeyDown;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            PreviewKeyDown -= Window3D_PreviewKeyDown;

            if (viewport != null)
            {
                viewport.Children.Clear();
                ContentGrid.Children.Remove(viewport);
                viewport = null;
            }

            if (modelVisual != null)
            {
                modelVisual.Content = null;
                modelVisual = null;
            }

            colormapMaterial = null;
            grayPixels = null;
            currentColormap = null;
        }

        private async void Window3D_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (viewport == null) return;

            if (e.Key == Key.Add)
            {
                heightScale *= 1.1;
                await UpdateMeshAsync();
            }
            else if (e.Key == Key.Subtract)
            {
                heightScale *= 0.9;
                await UpdateMeshAsync();
            }
            else if (e.Key == Key.L)
            {
                viewport.Camera.Position = new Point3D(Position.X - 10, Position.Y, Position.Z);
            }
            else if (e.Key == Key.A)
            {
                viewport.Camera.LookDirection = new Vector3D(Position.X, Position.Y, Position.Z + 10);
            }
            else if (e.Key == Key.R)
            {
                viewport.Camera.Position = new Point3D(Position.X + 10, Position.Y, Position.Z);
            }
            else if (e.Key == Key.B)
            {
                viewport.Camera.Position = new Point3D(Position.X, Position.Y - 10, Position.Z);
            }
        }

        private static int FindClosestFactor(int value, int[] factors)
        {
            int closest = factors[0];
            foreach (int factor in factors)
            {
                if (Math.Abs(value - factor) < Math.Abs(value - closest))
                    closest = factor;
            }
            return closest;
        }

        /// <summary>
        /// Convert WriteableBitmap to downsampled grayscale bytes (pure C#, handles all WPF pixel formats).
        /// </summary>
        private static (byte[] Gray, int Width, int Height) ConvertBitmapToGray(
            WriteableBitmap bitmap, int targetX, int targetY)
        {
            int origW = bitmap.PixelWidth;
            int origH = bitmap.PixelHeight;
            int targetPixels = targetX * targetY;

            double initScale = Math.Sqrt((double)origW * origH / targetPixels);
            int[] factors = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048 };
            int scaleFactor = Math.Max(FindClosestFactor((int)Math.Round(initScale), factors), 1);

            int newW = Math.Max(origW / scaleFactor, 2);
            int newH = Math.Max(origH / scaleFactor, 2);

            // WPF FormatConvertedBitmap handles all format conversions (16bit, float, BGR, etc.)
            var graySource = new FormatConvertedBitmap(bitmap, PixelFormats.Gray8, null, 0);
            int grayStride = origW; // Gray8 = 1 byte per pixel
            byte[] fullGray = new byte[origH * grayStride];
            graySource.CopyPixels(fullGray, grayStride, 0);

            // Nearest-neighbor downsample
            byte[] gray = new byte[newW * newH];
            for (int y = 0; y < newH; y++)
            {
                int srcY = y * scaleFactor;
                for (int x = 0; x < newW; x++)
                    gray[y * newW + x] = fullGray[srcY * grayStride + x * scaleFactor];
            }

            return (gray, newW, newH);
        }

        private void CreateColormapMaterial(byte[] lut)
        {
            if (grayPixels == null) return;

            byte[] texPixels = new byte[newWidth * newHeight * 3];
            for (int i = 0; i < grayPixels.Length; i++)
            {
                int lutIdx = grayPixels[i] * 3;
                texPixels[i * 3] = lut[lutIdx];
                texPixels[i * 3 + 1] = lut[lutIdx + 1];
                texPixels[i * 3 + 2] = lut[lutIdx + 2];
            }

            var bitmap = new WriteableBitmap(newWidth, newHeight, 96, 96, PixelFormats.Bgr24, null);
            bitmap.WritePixels(new Int32Rect(0, 0, newWidth, newHeight), texPixels, newWidth * 3, 0);
            bitmap.Freeze();

            var brush = new ImageBrush(bitmap);
            brush.Freeze();
            colormapMaterial = new DiffuseMaterial(brush);
        }

        private static (Point3D[] Positions, int[] Indices, Point[] TexCoords) BuildMeshArrays(
            byte[] pixels, int width, int height, double heightScale)
        {
            int vertexCount = width * height;
            int indexCount = (width - 1) * (height - 1) * 6;

            var positions = new Point3D[vertexCount];
            var texCoords = new Point[vertexCount];
            var indices = new int[indexCount];

            double wm1 = Math.Max(width - 1, 1);
            double hm1 = Math.Max(height - 1, 1);

            // Shared vertex grid
            for (int y = 0; y < height; y++)
            {
                int flippedY = height - 1 - y;
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = rowOffset + x;
                    double z = pixels[idx] / 255.0 * heightScale;
                    positions[idx] = new Point3D(x, flippedY, z);
                    texCoords[idx] = new Point(x / wm1, y / hm1);
                }
            }

            // Index buffer - two triangles per grid cell
            int ii = 0;
            for (int y = 0; y < height - 1; y++)
            {
                int rowStart = y * width;
                int nextRowStart = rowStart + width;
                for (int x = 0; x < width - 1; x++)
                {
                    int tl = rowStart + x;
                    int tr = tl + 1;
                    int bl = nextRowStart + x;
                    int br = bl + 1;

                    indices[ii++] = tl;
                    indices[ii++] = tr;
                    indices[ii++] = bl;
                    indices[ii++] = tr;
                    indices[ii++] = br;
                    indices[ii++] = bl;
                }
            }

            return (positions, indices, texCoords);
        }

        private DiffuseMaterial CurrentMaterial =>
            colormapMaterial ?? new DiffuseMaterial(Brushes.White);

        private async Task UpdateMeshAsync()
        {
            if (grayPixels == null || viewport == null) return;

            var pixels = grayPixels;
            int w = newWidth, h = newHeight;
            double hs = heightScale;

            var (positions, indices, texCoords) = await Task.Run(() => BuildMeshArrays(pixels, w, h, hs));

            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection(positions),
                TriangleIndices = new Int32Collection(indices),
                TextureCoordinates = new PointCollection(texCoords)
            };

            var material = CurrentMaterial;
            var model = new GeometryModel3D(mesh, material) { BackMaterial = material };

            if (modelVisual == null)
            {
                modelVisual = new ModelVisual3D { Content = model };
                viewport.Children.Add(modelVisual);
            }
            else
            {
                modelVisual.Content = model;
            }
        }

        private void UpdateMaterial()
        {
            if (modelVisual?.Content is GeometryModel3D model)
            {
                var material = CurrentMaterial;
                model.Material = material;
                model.BackMaterial = material;
            }
        }

        private void ComboBoxColormap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxColormap.SelectedItem is ColormapInfo info && grayPixels != null)
            {
                currentColormap = info;
                Config.SelectedColormap = info.Name;

                if (info.Lut != null)
                {
                    CreateColormapMaterial(info.Lut);
                    ColorBarPanel.Visibility = Visibility.Visible;
                    ColorBarImage.Source = info.ImageSource;
                }
                else
                {
                    colormapMaterial = null;
                    ColorBarPanel.Visibility = Visibility.Collapsed;
                }

                UpdateMaterial();
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            TxtTargetX.Text = Config.TargetPixelsX.ToString();
            TxtTargetY.Text = Config.TargetPixelsY.ToString();
            SettingsPopup.IsOpen = true;
        }

        private async void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtTargetX.Text, out int x) && int.TryParse(TxtTargetY.Text, out int y) && x > 0 && y > 0)
            {
                Config.TargetPixelsX = x;
                Config.TargetPixelsY = y;

                (grayPixels, newWidth, newHeight) = ConvertBitmapToGray(colorBitmap, x, y);
                if (grayPixels == null || grayPixels.Length == 0) return;

                if (currentColormap?.Lut != null)
                    CreateColormapMaterial(currentColormap.Lut);

                await UpdateMeshAsync();
            }
            SettingsPopup.IsOpen = false;
        }

        public Vector3D Position => viewport!.Camera.LookDirection;

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            heightScale *= 1.1;
            await UpdateMeshAsync();
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            heightScale *= 0.9;
            await UpdateMeshAsync();
        }

        private void L_Click(object sender, RoutedEventArgs e)
        {
            viewport!.Camera.Position = new Point3D(Position.X - 10, Position.Y, Position.Z);
        }

        private void R_Click(object sender, RoutedEventArgs e)
        {
            viewport!.Camera.Position = new Point3D(Position.X + 10, Position.Y, Position.Z);
        }

        private void T_Click(object sender, RoutedEventArgs e)
        {
            viewport!.Camera.Position = new Point3D(Position.X, Position.Y + 10, Position.Z);
        }

        private void B_Click(object sender, RoutedEventArgs e)
        {
            viewport!.Camera.Position = new Point3D(Position.X, Position.Y - 10, Position.Z);
        }

        private void D_Click(object sender, RoutedEventArgs e)
        {
            viewport!.Camera.LookDirection = new Vector3D(Position.X, Position.Y - 10, Position.Z);
        }

        private void F_Click(object sender, RoutedEventArgs e)
        {
            viewport!.Camera.LookDirection = new Vector3D(Position.X, Position.Y + 10, Position.Z);
        }

        private void A_Click(object sender, RoutedEventArgs e)
        {
            viewport!.Camera.LookDirection = new Vector3D(Position.X, Position.Y, Position.Z + 10);
        }

        private void C_Click(object sender, RoutedEventArgs e)
        {
            viewport!.Camera.LookDirection = new Vector3D(Position.X, Position.Y, Position.Z - 10);
        }
    }
}
