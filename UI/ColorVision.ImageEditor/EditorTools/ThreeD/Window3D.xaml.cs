using ColorVision.Common.MVVM;
using ColorVision.UI;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
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
        private MeshGeometry3D? currentMesh;
        private byte[]? grayPixels;
        private int newWidth;
        private int newHeight;
        private double heightScale = 100.0;
        private DiffuseMaterial? colormapMaterial;
        private ColormapInfo? currentColormap;

        // Cached initial camera state for reset
        private Point3D initialCameraPosition;
        private Vector3D initialLookDirection;
        private Vector3D initialUpDirection;

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

            // Calculate initial camera position
            initialCameraPosition = new Point3D(newWidth / 2.0, -newHeight * 0.8, newHeight * 0.6);
            initialLookDirection = new Vector3D(0, newHeight * 0.8, -newHeight * 0.6);
            initialUpDirection = new Vector3D(0, 0, 1);

            viewport = new HelixViewport3D
            {
                Camera = new PerspectiveCamera
                {
                    Position = initialCameraPosition,
                    LookDirection = initialLookDirection,
                    UpDirection = initialUpDirection,
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

            // Build mesh once with current height scale
            await BuildMeshAsync();
            viewport.CameraController.AddRotateForce(0, 4.5);

            // Setup input handling
            PreviewKeyDown += Window3D_PreviewKeyDown;
            viewport.MouseMove += Viewport_MouseMove;
            viewport.MouseLeave += Viewport_MouseLeave;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            PreviewKeyDown -= Window3D_PreviewKeyDown;

            if (viewport != null)
            {
                viewport.MouseMove -= Viewport_MouseMove;
                viewport.MouseLeave -= Viewport_MouseLeave;
                viewport.Children.Clear();
                ContentGrid.Children.Remove(viewport);
                viewport = null;
            }

            if (modelVisual != null)
            {
                modelVisual.Content = null;
                modelVisual = null;
            }

            currentMesh = null;
            colormapMaterial = null;
            grayPixels = null;
            currentColormap = null;
        }

        private void Window3D_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (viewport == null) return;

            const double moveSpeed = 20.0;
            const double lookSpeed = 10.0;

            switch (e.Key)
            {
                // Height scale
                case Key.Add:
                case Key.OemPlus:
                    heightScale *= 1.1;
                    UpdateMeshPositions();
                    e.Handled = true;
                    break;
                case Key.Subtract:
                case Key.OemMinus:
                    heightScale *= 0.9;
                    UpdateMeshPositions();
                    e.Handled = true;
                    break;

                // Camera position (WASD-like with T/B)
                case Key.L: // Left
                    viewport.Camera.Position = new Point3D(
                        viewport.Camera.Position.X - moveSpeed,
                        viewport.Camera.Position.Y,
                        viewport.Camera.Position.Z);
                    e.Handled = true;
                    break;
                case Key.T: // Forward (Top view direction in default orientation)
                    viewport.Camera.Position = new Point3D(
                        viewport.Camera.Position.X,
                        viewport.Camera.Position.Y + moveSpeed,
                        viewport.Camera.Position.Z);
                    e.Handled = true;
                    break;
                case Key.R: // Right
                    viewport.Camera.Position = new Point3D(
                        viewport.Camera.Position.X + moveSpeed,
                        viewport.Camera.Position.Y,
                        viewport.Camera.Position.Z);
                    e.Handled = true;
                    break;
                case Key.B: // Backward
                    viewport.Camera.Position = new Point3D(
                        viewport.Camera.Position.X,
                        viewport.Camera.Position.Y - moveSpeed,
                        viewport.Camera.Position.Z);
                    e.Handled = true;
                    break;

                // Look direction (arrow-like with A/C)
                case Key.A: // Look Up (increase Z component of look direction)
                    viewport.Camera.LookDirection = new Vector3D(
                        viewport.Camera.LookDirection.X,
                        viewport.Camera.LookDirection.Y,
                        viewport.Camera.LookDirection.Z + lookSpeed);
                    e.Handled = true;
                    break;
                case Key.C: // Look Down
                    viewport.Camera.LookDirection = new Vector3D(
                        viewport.Camera.LookDirection.X,
                        viewport.Camera.LookDirection.Y,
                        viewport.Camera.LookDirection.Z - lookSpeed);
                    e.Handled = true;
                    break;
                case Key.D: // Look Left (decrease X)
                    viewport.Camera.LookDirection = new Vector3D(
                        viewport.Camera.LookDirection.X - lookSpeed,
                        viewport.Camera.LookDirection.Y,
                        viewport.Camera.LookDirection.Z);
                    e.Handled = true;
                    break;
                case Key.F: // Look Right (increase X)
                    viewport.Camera.LookDirection = new Vector3D(
                        viewport.Camera.LookDirection.X + lookSpeed,
                        viewport.Camera.LookDirection.Y,
                        viewport.Camera.LookDirection.Z);
                    e.Handled = true;
                    break;

                // Reset view
                case Key.Home:
                    ResetCameraView();
                    e.Handled = true;
                    break;
            }
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (viewport == null || grayPixels == null) return;

            // Show coordinate in status/tooltip
            UpdateHoverTooltip(e);
        }

        private void Viewport_MouseLeave(object sender, MouseEventArgs e)
        {
            HoverInfoPopup.IsOpen = false;
        }

        private void UpdateHoverTooltip(MouseEventArgs e)
        {
            if (viewport?.Camera == null || grayPixels == null) return;

            // Project mouse position to approximate 3D coordinates
            // This is a simplified approximation
            var pos = e.GetPosition(viewport);
            var tooltipText = $"Mouse: ({pos.X:F0}, {pos.Y:F0})\n";
            tooltipText += $"Scale: {heightScale:F1}x\n";
            tooltipText += $"Res: {newWidth}x{newHeight}";

            HoverInfoText.Text = tooltipText;
            HoverInfoPopup.IsOpen = true;
        }

        private void ResetCameraView()
        {
            if (viewport?.Camera == null) return;

            viewport.Camera.Position = initialCameraPosition;
            viewport.Camera.LookDirection = initialLookDirection;
            viewport.Camera.UpDirection = initialUpDirection;
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

        /// <summary>
        /// Build mesh arrays - positions, indices, and texture coordinates.
        /// Separated from mesh creation to allow position-only updates.
        /// </summary>
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

        /// <summary>
        /// Update only vertex positions when height scale changes - much faster than rebuilding entire mesh.
        /// </summary>
        private void UpdateMeshPositions()
        {
            if (currentMesh == null || grayPixels == null) return;

            int vertexCount = newWidth * newHeight;
            var newPositions = new Point3DCollection(vertexCount);

            for (int y = 0; y < newHeight; y++)
            {
                int flippedY = newHeight - 1 - y;
                int rowOffset = y * newWidth;
                for (int x = 0; x < newWidth; x++)
                {
                    int idx = rowOffset + x;
                    double z = grayPixels[idx] / 255.0 * heightScale;
                    newPositions.Add(new Point3D(x, flippedY, z));
                }
            }

            currentMesh.Positions = newPositions;
        }

        private DiffuseMaterial CurrentMaterial =>
            colormapMaterial ?? new DiffuseMaterial(Brushes.White);

        /// <summary>
        /// Build mesh initially - creates the mesh structure with positions, indices, and UVs.
        /// </summary>
        private async Task BuildMeshAsync()
        {
            if (grayPixels == null || viewport == null) return;

            var pixels = grayPixels;
            int w = newWidth, h = newHeight;
            double hs = heightScale;

            var (positions, indices, texCoords) = await Task.Run(() => BuildMeshArrays(pixels, w, h, hs));

            currentMesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection(positions),
                TriangleIndices = new Int32Collection(indices),
                TextureCoordinates = new PointCollection(texCoords)
            };

            var material = CurrentMaterial;
            var model = new GeometryModel3D(currentMesh, material) { BackMaterial = material };

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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
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

                // Reset mesh on resolution change (need to rebuild)
                currentMesh = null;

                if (currentColormap?.Lut != null)
                    CreateColormapMaterial(currentColormap.Lut);

                await BuildMeshAsync();
            }
            SettingsPopup.IsOpen = false;
        }

        private void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            SaveScreenshot();
        }

        private void ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            ResetCameraView();
        }

        /// <summary>
        /// Save a screenshot of the current 3D viewport.
        /// </summary>
        private void SaveScreenshot()
        {
            if (viewport == null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp",
                DefaultExt = "png",
                FileName = $"3DView_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Render the viewport to a bitmap
                    var renderBitmap = new RenderTargetBitmap(
                        (int)viewport.ActualWidth,
                        (int)viewport.ActualHeight,
                        96, 96,
                        PixelFormats.Pbgra32);

                    renderBitmap.Render(viewport);

                    // Encode and save
                    BitmapEncoder encoder = dialog.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        ? new JpegBitmapEncoder()
                        : dialog.FileName.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
                            ? new BmpBitmapEncoder()
                            : new PngBitmapEncoder();

                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                    using var stream = File.Create(dialog.FileName);
                    encoder.Save(stream);

                    MessageBox.Show($"Screenshot saved:\n{dialog.FileName}", "Screenshot Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save screenshot:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Height scale adjustment buttons
        private async void HeightScaleIncrease_Click(object sender, RoutedEventArgs e)
        {
            heightScale *= 1.1;
            UpdateMeshPositions();
        }

        private async void HeightScaleDecrease_Click(object sender, RoutedEventArgs e)
        {
            heightScale *= 0.9;
            UpdateMeshPositions();
        }

        // Camera movement buttons
        private void CameraMoveLeft_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera == null) return;
            viewport.Camera.Position = new Point3D(
                viewport.Camera.Position.X - 20,
                viewport.Camera.Position.Y,
                viewport.Camera.Position.Z);
        }

        private void CameraMoveForward_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera == null) return;
            viewport.Camera.Position = new Point3D(
                viewport.Camera.Position.X,
                viewport.Camera.Position.Y + 20,
                viewport.Camera.Position.Z);
        }

        private void CameraMoveRight_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera == null) return;
            viewport.Camera.Position = new Point3D(
                viewport.Camera.Position.X + 20,
                viewport.Camera.Position.Y,
                viewport.Camera.Position.Z);
        }

        private void CameraMoveBack_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera == null) return;
            viewport.Camera.Position = new Point3D(
                viewport.Camera.Position.X,
                viewport.Camera.Position.Y - 20,
                viewport.Camera.Position.Z);
        }

        // Look direction buttons
        private void LookLeft_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera == null) return;
            viewport.Camera.LookDirection = new Vector3D(
                viewport.Camera.LookDirection.X - 10,
                viewport.Camera.LookDirection.Y,
                viewport.Camera.LookDirection.Z);
        }

        private void LookUp_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera == null) return;
            viewport.Camera.LookDirection = new Vector3D(
                viewport.Camera.LookDirection.X,
                viewport.Camera.LookDirection.Y,
                viewport.Camera.LookDirection.Z + 10);
        }

        private void LookRight_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera == null) return;
            viewport.Camera.LookDirection = new Vector3D(
                viewport.Camera.LookDirection.X + 10,
                viewport.Camera.LookDirection.Y,
                viewport.Camera.LookDirection.Z);
        }

        private void LookDown_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera == null) return;
            viewport.Camera.LookDirection = new Vector3D(
                viewport.Camera.LookDirection.X,
                viewport.Camera.LookDirection.Y,
                viewport.Camera.LookDirection.Z - 10);
        }
    }
}
