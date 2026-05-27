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
using Viewport3DHelper = ColorVision.ImageEditor.EditorTools.ThreeD.Viewport3DHelper;

namespace ColorVision.ImageEditor
{
    public partial class Window3D : Window
    {
        private readonly WriteableBitmap colorBitmap;
        private readonly double? initialHeightScaleOverride;
        private HelixViewport3D? viewport;
        private ModelVisual3D? modelVisual;
        private MeshGeometry3D? currentMesh;
        private byte[]? grayPixels;
        private byte[]? alphaPixels;
        private int newWidth;
        private int newHeight;
        private double heightScale = 100.0;
        private DiffuseMaterial? colormapMaterial;
        private ColormapInfo? currentColormap;
        private List<Visual3D>? axesVisuals;
        private Point lastMousePosition;
        private bool isLeftButtonDown;
        private Transform3DGroup currentTransform = new Transform3DGroup();
        private QuaternionRotation3D currentRotation = new QuaternionRotation3D();
        private Point3D rotationCenter;
        private bool hasRotationCenter;

        // Cached initial camera state for reset
        private Point3D initialCameraPosition;
        private Vector3D initialLookDirection;
        private Vector3D initialUpDirection;

        // Mouse move throttling
        private DateTime lastMouseMoveTime = DateTime.MinValue;
        private readonly TimeSpan mouseMoveThrottle = TimeSpan.FromMilliseconds(16);

        // Reusable position collection to avoid GC pressure
        private Point3DCollection? cachedPositions;

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

        public Window3D(WriteableBitmap writeableBitmap, double? initialHeightScaleOverride = null)
        {
            colorBitmap = writeableBitmap;
            this.initialHeightScaleOverride = initialHeightScaleOverride;
            InitializeComponent();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            heightScale = initialHeightScaleOverride.HasValue && initialHeightScaleOverride.Value > 0
                ? initialHeightScaleOverride.Value
                : Config.DefaultHeightScale;

            (grayPixels, alphaPixels, newWidth, newHeight) = ConvertBitmapToGray(
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
            initialCameraPosition = new Point3D(newWidth * 0.92, -newHeight * 1.35, newHeight * 1.15);
            initialLookDirection = new Vector3D(-newWidth * 0.42, newHeight * 1.35, -newHeight * 0.95);
            initialUpDirection = new Vector3D(0, 0, 1);

            viewport = Viewport3DHelper.CreateDefaultViewport(
                initialCameraPosition,
                initialLookDirection,
                initialUpDirection,
                60);

            Viewport3DHelper.AddCameraAlignedLights(viewport, Rect3D.Empty);
            ContentGrid.Children.Add(viewport);

            viewport.MouseDown += Viewport_ObjectRotateMouseDown;
            viewport.MouseMove += Viewport_ObjectRotateMouseMove;
            viewport.MouseUp += Viewport_ObjectRotateMouseUp;

            axesVisuals = Viewport3DHelper.CreateFixedCornerAxes(20);
            foreach (var axis in axesVisuals)
                viewport.Children.Add(axis);

            CompositionTarget.Rendering += CompositionTarget_Rendering;

            // Build mesh once with current height scale
            await BuildMeshAsync();

            // Setup input handling
            PreviewKeyDown += Window3D_PreviewKeyDown;
            viewport.MouseMove += Viewport_MouseMove;
            viewport.MouseLeave += Viewport_MouseLeave;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            PreviewKeyDown -= Window3D_PreviewKeyDown;
            CompositionTarget.Rendering -= CompositionTarget_Rendering;

            if (viewport != null)
            {
                viewport.MouseDown -= Viewport_ObjectRotateMouseDown;
                viewport.MouseMove -= Viewport_ObjectRotateMouseMove;
                viewport.MouseUp -= Viewport_ObjectRotateMouseUp;
                viewport.MouseMove -= Viewport_MouseMove;
                viewport.MouseLeave -= Viewport_MouseLeave;
                viewport.Children.Clear();
                ContentGrid.Children.Remove(viewport);
                viewport = null;
            }

            if (modelVisual != null)
            {
                if (modelVisual.Content is GeometryModel3D geometry)
                {
                    if (geometry.Geometry is MeshGeometry3D mesh)
                    {
                        mesh.Positions = null;
                        mesh.TriangleIndices = null;
                        mesh.TextureCoordinates = null;
                        mesh.Normals = null;
                    }
                    geometry.Geometry = null;
                    geometry.Material = null;
                    geometry.BackMaterial = null;
                }
                modelVisual.Content = null;
                modelVisual = null;
            }

            currentMesh = null;
            cachedPositions = null;
            colormapMaterial = null;
            grayPixels = null;
            alphaPixels = null;
            currentColormap = null;
            axesVisuals = null;

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
        }

        private void Window3D_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (viewport?.Camera is not ProjectionCamera camera) return;

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

                // Reset view
                case Key.Home:
                    ResetCameraView();
                    e.Handled = true;
                    break;

                default:
                    e.Handled = Viewport3DHelper.HandleCameraKey(camera, e.Key);
                    break;
            }
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (viewport?.Camera is ProjectionCamera camera && axesVisuals != null)
                Viewport3DHelper.UpdateFixedCornerAxes(axesVisuals, camera);
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (viewport == null || grayPixels == null) return;

            // Throttle mouse move updates to ~60fps max to reduce unnecessary UI updates
            var now = DateTime.Now;
            if (now - lastMouseMoveTime < mouseMoveThrottle) return;
            lastMouseMoveTime = now;

            UpdateHoverTooltip(e);
        }

        private void Viewport_MouseLeave(object sender, MouseEventArgs e)
        {
            HoverInfoPopup.IsOpen = false;
        }

        private void UpdateHoverTooltip(MouseEventArgs e)
        {
            if (viewport?.Camera == null || grayPixels == null) return;

            var pos = e.GetPosition(viewport);
            Point3D? hitPoint = null;
            byte hitValue = 0;

            // Perform 3D hit test to find the point under the mouse
            HitTestResult result = VisualTreeHelper.HitTest(viewport, pos);
            if (result is RayMeshGeometry3DHitTestResult meshHit)
            {
                hitPoint = meshHit.PointHit;

                // Map 3D point back to grid coordinates
                int gridX = (int)Math.Clamp(Math.Round(hitPoint.Value.X), 0, newWidth - 1);
                int gridY = newHeight - 1 - (int)Math.Clamp(Math.Round(hitPoint.Value.Y), 0, newHeight - 1);
                int idx = gridY * newWidth + gridX;
                if (idx >= 0 && idx < grayPixels.Length)
                    hitValue = grayPixels[idx];
            }

            string tooltipText;
            if (hitPoint.HasValue)
            {
                var p = hitPoint.Value;
                tooltipText = $"X: {p.X:F1}  Y: {newHeight - 1 - p.Y:F1}\n";
                tooltipText += $"Z: {p.Z:F2}  {Properties.Resources.ThreeD_Value}: {hitValue}\n";
                tooltipText += $"{Properties.Resources.ThreeD_Height}: {heightScale:F1}x  {Properties.Resources.ThreeD_Resolution}: {newWidth}x{newHeight}";
            }
            else
            {
                tooltipText = $"{Properties.Resources.ThreeD_Mouse}: ({pos.X:F0}, {pos.Y:F0})\n";
                tooltipText += $"{Properties.Resources.ThreeD_Height}: {heightScale:F1}x  {Properties.Resources.ThreeD_Resolution}: {newWidth}x{newHeight}";
            }

            HoverInfoText.Text = tooltipText;
            HoverInfoPopup.IsOpen = true;
        }

        private void Viewport_ObjectRotateMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || modelVisual == null) return;
            isLeftButtonDown = true;
            lastMousePosition = e.GetPosition(viewport);
            viewport?.CaptureMouse();
            e.Handled = true;
        }

        private void Viewport_ObjectRotateMouseMove(object sender, MouseEventArgs e)
        {
            if (!isLeftButtonDown || modelVisual == null || viewport == null) return;

            Point currentPosition = e.GetPosition(viewport);
            Vector delta = currentPosition - lastMousePosition;
            lastMousePosition = currentPosition;

            const double rotationSpeed = 0.45;
            Quaternion yaw = new Quaternion(new Vector3D(0, 0, 1), delta.X * rotationSpeed);
            Quaternion pitch = new Quaternion(new Vector3D(1, 0, 0), delta.Y * rotationSpeed);
            currentRotation.Quaternion = yaw * pitch * currentRotation.Quaternion;
            modelVisual.Transform = currentTransform;
            e.Handled = true;
        }

        private void Viewport_ObjectRotateMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            isLeftButtonDown = false;
            viewport?.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void ResetCameraView()
        {
            if (viewport?.Camera is not PerspectiveCamera camera) return;
            Viewport3DHelper.ResetCameraView(camera, initialCameraPosition, initialLookDirection, initialUpDirection);
        }

        private void ResetModelRotation()
        {
            currentRotation = new QuaternionRotation3D(Quaternion.Identity);
            currentTransform = new Transform3DGroup();
            if (hasRotationCenter)
            {
                currentTransform.Children.Add(new TranslateTransform3D(-rotationCenter.X, -rotationCenter.Y, -rotationCenter.Z));
                currentTransform.Children.Add(new RotateTransform3D(currentRotation));
                currentTransform.Children.Add(new TranslateTransform3D(rotationCenter.X, rotationCenter.Y, rotationCenter.Z));
            }
            else
            {
                currentTransform.Children.Add(new RotateTransform3D(currentRotation));
            }
            if (modelVisual != null)
                modelVisual.Transform = currentTransform;
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

                (grayPixels, alphaPixels, newWidth, newHeight) = ConvertBitmapToGray(colorBitmap, x, y);
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
            if (viewport != null)
                Viewport3DHelper.SaveScreenshot(viewport, $"3DView_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        }

        private void ExportModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentMesh == null) return;

            var material = CurrentMaterial;
            var model = new GeometryModel3D(currentMesh, material) { BackMaterial = material };
            Viewport3DHelper.ExportModel(model, $"3DView_{DateTime.Now:yyyyMMdd_HHmmss}.obj");
        }

        private void ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            ResetModelRotation();
            ResetCameraView();
        }

        // Height scale adjustment buttons
        private void HeightScaleIncrease_Click(object sender, RoutedEventArgs e)
        {
            heightScale *= 1.1;
            UpdateMeshPositions();
        }

        private void HeightScaleDecrease_Click(object sender, RoutedEventArgs e)
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
        private static (byte[] Gray, byte[]? Alpha, int Width, int Height) ConvertBitmapToGray(
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

            var colorSource = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            int colorStride = origW * 4;
            byte[] fullColor = new byte[origH * colorStride];
            colorSource.CopyPixels(fullColor, colorStride, 0);

            byte[] fullGray = new byte[origW * origH];
            byte[] fullAlpha = new byte[origW * origH];
            bool hasTransparency = false;

            for (int i = 0; i < fullGray.Length; i++)
            {
                int offset = i * 4;
                byte blue = fullColor[offset];
                byte green = fullColor[offset + 1];
                byte red = fullColor[offset + 2];
                byte alpha = fullColor[offset + 3];

                fullGray[i] = (byte)Math.Clamp(Math.Round(blue * 0.114 + green * 0.587 + red * 0.299), 0, 255);
                fullAlpha[i] = alpha;
                hasTransparency |= alpha < 255;
            }

            // Bilinear interpolation downsample
            byte[] gray = new byte[newW * newH];
            byte[]? alphaMask = hasTransparency ? new byte[newW * newH] : null;
            double scaleX = (double)(origW - 1) / Math.Max(newW - 1, 1);
            double scaleY = (double)(origH - 1) / Math.Max(newH - 1, 1);

            for (int y = 0; y < newH; y++)
            {
                double srcY = y * scaleY;
                int y0 = (int)srcY;
                int y1 = Math.Min(y0 + 1, origH - 1);
                double fy = srcY - y0;

                for (int x = 0; x < newW; x++)
                {
                    double srcX = x * scaleX;
                    int x0 = (int)srcX;
                    int x1 = Math.Min(x0 + 1, origW - 1);
                    double fx = srcX - x0;

                    int idx00 = y0 * origW + x0;
                    int idx10 = y0 * origW + x1;
                    int idx01 = y1 * origW + x0;
                    int idx11 = y1 * origW + x1;

                    double v00 = fullGray[idx00];
                    double v10 = fullGray[idx10];
                    double v01 = fullGray[idx01];
                    double v11 = fullGray[idx11];

                    double value = v00 * (1 - fx) * (1 - fy)
                                 + v10 * fx * (1 - fy)
                                 + v01 * (1 - fx) * fy
                                 + v11 * fx * fy;

                    gray[y * newW + x] = (byte)Math.Clamp(Math.Round(value), 0, 255);

                    if (alphaMask != null)
                    {
                        double a00 = fullAlpha[idx00];
                        double a10 = fullAlpha[idx10];
                        double a01 = fullAlpha[idx01];
                        double a11 = fullAlpha[idx11];
                        double alphaValue = a00 * (1 - fx) * (1 - fy)
                                          + a10 * fx * (1 - fy)
                                          + a01 * (1 - fx) * fy
                                          + a11 * fx * fy;
                        alphaMask[y * newW + x] = (byte)Math.Clamp(Math.Round(alphaValue), 0, 255);
                    }
                }
            }

            return (gray, alphaMask, newW, newH);
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
        /// Build mesh arrays - positions, indices, texture coordinates, and normals.
        /// Separated from mesh creation to allow position-only updates.
        /// </summary>
        private static (Point3D[] Positions, int[] Indices, Point[] TexCoords, Vector3D[] Normals) BuildMeshArrays(
            byte[] pixels, byte[]? alphaMask, int width, int height, double heightScale)
        {
            int vertexCount = width * height;
            int visibleQuadCount = 0;
            if (alphaMask == null)
            {
                visibleQuadCount = (width - 1) * (height - 1);
            }
            else
            {
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
                        if (IsQuadVisible(alphaMask, tl, tr, bl, br))
                        {
                            visibleQuadCount++;
                        }
                    }
                }
            }

            int indexCount = visibleQuadCount * 6;

            var positions = new Point3D[vertexCount];
            var texCoords = new Point[vertexCount];
            var normals = new Vector3D[vertexCount];
            var indices = new int[indexCount];

            double wm1 = Math.Max(width - 1, 1);
            double hm1 = Math.Max(height - 1, 1);
            double centerX = wm1 / 2.0;
            double centerY = hm1 / 2.0;

            // Shared vertex grid
            for (int y = 0; y < height; y++)
            {
                int flippedY = height - 1 - y;
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = rowOffset + x;
                    texCoords[idx] = new Point(x / wm1, y / hm1);

                    if (!IsVertexVisible(alphaMask, idx))
                    {
                        positions[idx] = new Point3D(centerX, centerY, 0);
                        normals[idx] = new Vector3D(0, 0, 1);
                        continue;
                    }

                    double z = pixels[idx] / 255.0 * heightScale;
                    positions[idx] = new Point3D(x, flippedY, z);
                }
            }

            // Compute per-vertex normals from height gradients
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = rowOffset + x;
                    if (!IsVertexVisible(alphaMask, idx))
                    {
                        normals[idx] = new Vector3D(0, 0, 1);
                        continue;
                    }

                    double zL = x > 0 ? pixels[idx - 1] / 255.0 * heightScale : positions[idx].Z;
                    double zR = x < width - 1 ? pixels[idx + 1] / 255.0 * heightScale : positions[idx].Z;
                    double zU = y > 0 ? pixels[idx - width] / 255.0 * heightScale : positions[idx].Z;
                    double zD = y < height - 1 ? pixels[idx + width] / 255.0 * heightScale : positions[idx].Z;

                    // Gradient in X, gradient in Y (flipped), cross product gives normal
                    double dzdx = (zR - zL) * 0.5;
                    double dzdy = (zD - zU) * 0.5;
                    var normal = new Vector3D(-dzdx, dzdy, 1.0);
                    normal.Normalize();
                    normals[idx] = normal;
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

                    if (!IsQuadVisible(alphaMask, tl, tr, bl, br))
                    {
                        continue;
                    }

                    indices[ii++] = tl;
                    indices[ii++] = tr;
                    indices[ii++] = bl;
                    indices[ii++] = tr;
                    indices[ii++] = br;
                    indices[ii++] = bl;
                }
            }

            return (positions, indices, texCoords, normals);
        }

        private static bool IsVertexVisible(byte[]? alphaMask, int index)
        {
            return alphaMask == null || alphaMask[index] > 127;
        }

        private static bool IsQuadVisible(byte[]? alphaMask, int topLeft, int topRight, int bottomLeft, int bottomRight)
        {
            return alphaMask == null
                || (alphaMask[topLeft] > 127
                    && alphaMask[topRight] > 127
                    && alphaMask[bottomLeft] > 127
                    && alphaMask[bottomRight] > 127);
        }

        /// <summary>
        /// Update only vertex positions when height scale changes - much faster than rebuilding entire mesh.
        /// Reuses the Point3DCollection to avoid GC pressure.
        /// </summary>
        private void UpdateMeshPositions()
        {
            if (currentMesh == null || grayPixels == null) return;

            if (cachedPositions == null)
                cachedPositions = new Point3DCollection(newWidth * newHeight);
            else
                cachedPositions.Clear();

            var normals = new Vector3DCollection(newWidth * newHeight);
            double centerX = Math.Max(newWidth - 1, 1) / 2.0;
            double centerY = Math.Max(newHeight - 1, 1) / 2.0;

            for (int y = 0; y < newHeight; y++)
            {
                int flippedY = newHeight - 1 - y;
                int rowOffset = y * newWidth;
                for (int x = 0; x < newWidth; x++)
                {
                    int idx = rowOffset + x;

                    if (!IsVertexVisible(alphaPixels, idx))
                    {
                        cachedPositions.Add(new Point3D(centerX, centerY, 0));
                        normals.Add(new Vector3D(0, 0, 1));
                        continue;
                    }

                    double z = grayPixels[idx] / 255.0 * heightScale;
                    cachedPositions.Add(new Point3D(x, flippedY, z));

                    // Recompute normal from height gradients
                    double zL = x > 0 ? grayPixels[idx - 1] / 255.0 * heightScale : z;
                    double zR = x < newWidth - 1 ? grayPixels[idx + 1] / 255.0 * heightScale : z;
                    double zU = y > 0 ? grayPixels[idx - newWidth] / 255.0 * heightScale : z;
                    double zD = y < newHeight - 1 ? grayPixels[idx + newWidth] / 255.0 * heightScale : z;

                    double dzdx = (zR - zL) * 0.5;
                    double dzdy = (zD - zU) * 0.5;
                    var normal = new Vector3D(-dzdx, dzdy, 1.0);
                    normal.Normalize();
                    normals.Add(normal);
                }
            }

            currentMesh.Positions = cachedPositions;
            currentMesh.Normals = normals;
            UpdateRotationCenterFromMesh(currentMesh);
            ResetModelRotation();
        }

        private void UpdateRotationCenterFromMesh(MeshGeometry3D mesh)
        {
            if (mesh.Positions.Count == 0)
            {
                hasRotationCenter = false;
                return;
            }

            Rect3D bounds = mesh.Bounds;
            if (bounds.IsEmpty)
            {
                hasRotationCenter = false;
                return;
            }

            rotationCenter = new Point3D(
                bounds.X + bounds.SizeX / 2,
                bounds.Y + bounds.SizeY / 2,
                bounds.Z + bounds.SizeZ / 2);
            hasRotationCenter = true;
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
            var alpha = alphaPixels;
            int w = newWidth, h = newHeight;
            double hs = heightScale;

            var (positions, indices, texCoords, normals) = await Task.Run(() => BuildMeshArrays(pixels, alpha, w, h, hs));

            currentMesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection(positions),
                TriangleIndices = new Int32Collection(indices),
                TextureCoordinates = new PointCollection(texCoords),
                Normals = new Vector3DCollection(normals)
            };

            UpdateRotationCenterFromMesh(currentMesh);
            ResetModelRotation();

            var material = CurrentMaterial;
            var model = new GeometryModel3D(currentMesh, material) { BackMaterial = material };

            if (modelVisual == null)
            {
                ResetModelRotation();
                modelVisual = new ModelVisual3D { Content = model, Transform = currentTransform };
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

    }
}
