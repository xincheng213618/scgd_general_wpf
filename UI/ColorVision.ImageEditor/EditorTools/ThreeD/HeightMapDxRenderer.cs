using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Shaders;
using HelixToolkit.Wpf.SharpDX;
using log4net;
using SharpDX.Direct3D11;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using DxMesh = HelixToolkit.SharpDX.MeshGeometry3D;
using DxCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;
using WpfMesh = System.Windows.Media.Media3D.MeshGeometry3D;
using GeometryModel3D = System.Windows.Media.Media3D.GeometryModel3D;
using Color = System.Windows.Media.Color;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    internal readonly record struct HeightMapDxBuildMetrics(int DetailVertices, int DetailTriangles,
        int InteractionVertices, int InteractionTriangles, double BuildMilliseconds);

    internal readonly record struct HeightMapHit(Point3D Position, int SampleX, int SampleY, byte Gray);

    /// <summary>
    /// Direct3D 11 height-field view. Two immutable buffers stay attached while camera/height updates
    /// change only transforms and visibility. It never allocates a mesh during an interaction.
    /// </summary>
    internal sealed class HeightMapDxRenderer : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HeightMapDxRenderer));
        private readonly DefaultEffectsManager effectsManager;
        private readonly PhongMaterial material;
        private readonly MeshGeometryModel3D detailModel;
        private readonly MeshGeometryModel3D interactionModel;
        private readonly ScaleTransform3D heightTransform = new(1, 1, 100);
        private readonly AmbientLight3D ambientLight;
        private readonly DirectionalLight3D keyLight;
        private readonly DirectionalLight3D fillLight;
        private HeightMapDxGeometry? detailGeometry;
        private HeightMapDxGeometry? interactionGeometry;
        private byte[]? currentLut;
        private double heightScale = 100;
        private bool isInteracting;
        private bool isDisposed;
        private long loadVersion;
        private double minimumGray;
        private double maximumGray;

        public Viewport3DX Viewport { get; }
        public DxCamera Camera { get; }
        public HeightMapSample Sample => detailGeometry?.Sample ?? default;
        public HeightMapSample ActiveSample => isInteracting && interactionModel.Geometry != null
            ? interactionGeometry!.Sample : Sample;
        public double WorldWidth { get; private set; }
        public double WorldHeight { get; private set; }
        public int InteractionMaxWidth { get; set; } = 512;
        public int InteractionMaxHeight { get; set; } = 512;
        public Rect3D Bounds => detailGeometry == null || detailGeometry.Indices.Length == 0 ? Rect3D.Empty : new Rect3D(0, 0,
            minimumGray * heightScale, WorldWidth, WorldHeight, (maximumGray - minimumGray) * heightScale);
        public HeightMapDxBuildMetrics LastBuildMetrics { get; private set; }
        public HeightMapDxGeometry ExportGeometry => detailGeometry ?? throw new InvalidOperationException("No height field is loaded.");
        public byte[]? ExportLut => currentLut;

        public HeightMapDxRenderer()
        {
            effectsManager = new DefaultEffectsManager();
            Camera = new DxCamera { Position = new Point3D(600, -500, 600), LookDirection = new Vector3D(-350, 750, -550),
                UpDirection = new Vector3D(0, 0, 1), FieldOfView = 45, NearPlaneDistance = 0.1, FarPlaneDistance = 10000 };
            Viewport = new Viewport3DX { EffectsManager = effectsManager, Camera = Camera,
                BackgroundColor = System.Windows.Media.Color.FromRgb(23, 27, 34), ModelUpDirection = new Vector3D(0, 0, 1),
                IsRotationEnabled = false, IsPanEnabled = false, IsZoomEnabled = false, IsMoveEnabled = false,
                IsInertiaEnabled = false, EnableMouseButtonHitTest = false, ShowViewCube = false,
                ShowCoordinateSystem = true, CoordinateSystemLabelX = "X", CoordinateSystemLabelY = "Y",
                CoordinateSystemLabelZ = "灰度", CoordinateSystemLabelForeground = Colors.LightGray,
                CoordinateSystemSize = 0.7, CoordinateSystemHorizontalPosition = -0.82,
                CoordinateSystemVerticalPosition = -0.8, IsCoordinateSystemMoverEnabled = false,
                EnableSwapChainRendering = false, BelongsToParentWindow = false, FXAALevel = FXAALevel.Low };
            material = new PhongMaterial { DiffuseColor = new Color4(1, 1, 1, 1), AmbientColor = new Color4(0, 0, 0, 1),
                SpecularColor = new Color4(0.08f, 0.08f, 0.08f, 1), SpecularShininess = 28,
                DiffuseMapSampler = DefaultSamplers.LinearSamplerClampAni4,
                // Helix's default VS transforms normals by world, not inverse-transpose. World-space
                // derivatives retain correct illumination when the user changes only the Z scale.
                EnableFlatShading = true };
            detailModel = CreateModel();
            interactionModel = CreateModel();
            ambientLight = new AmbientLight3D();
            keyLight = new DirectionalLight3D { Direction = new Vector3D(-0.4, 0.5, -1) };
            fillLight = new DirectionalLight3D { Direction = new Vector3D(0.8, -0.5, -0.4) };
            Viewport.Items.Add(ambientLight);
            Viewport.Items.Add(keyLight);
            Viewport.Items.Add(fillLight);
            Viewport.Items.Add(detailModel);
            Viewport.Items.Add(interactionModel);
            SetLighting(0.48, 0.62);
        }

        private MeshGeometryModel3D CreateModel() => new() { Material = material, Transform = heightTransform,
            CullMode = CullMode.None, IsHitTestVisible = false, IsRendering = false };

        public async Task<HeightMapDxBuildMetrics> LoadAsync(HeightMapSample sample, double worldWidth,
            double worldHeight, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (sample.Width < 2 || sample.Height < 2 || sample.Gray.Length != checked(sample.Width * sample.Height)
                || sample.Alpha != null && sample.Alpha.Length != sample.Gray.Length)
                throw new ArgumentException("The height field must contain a complete grid of at least 2 × 2 samples.", nameof(sample));
            if (!double.IsFinite(worldWidth) || !double.IsFinite(worldHeight) || worldWidth <= 0 || worldHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(worldWidth));
            long version = ++loadVersion;
            int lowWidth = Math.Clamp(InteractionMaxWidth, 2, 1024);
            int lowHeight = Math.Clamp(InteractionMaxHeight, 2, 1024);
            var result = await Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                var high = HeightMapDxGeometry.Build(sample, worldWidth, worldHeight, cancellationToken);
                HeightMapSample reduced = HeightMapDxGeometry.Reduce(sample, lowWidth, lowHeight, cancellationToken);
                var low = reduced.Width == sample.Width && reduced.Height == sample.Height ? high
                    : HeightMapDxGeometry.Build(reduced, worldWidth, worldHeight, cancellationToken, sample);
                double min = 255, max = 0;
                for (int i = 0; i < sample.Gray.Length; i++)
                    if (sample.Alpha == null || sample.Alpha[i] > 127) { min = Math.Min(min, sample.Gray[i]); max = Math.Max(max, sample.Gray[i]); }
                // Helix geometry and numeric collections are thread-neutral. Include their copies in
                // measured CPU preparation instead of doing large collection copies on the UI thread.
                DxMesh highMesh = CreateDxMesh(high);
                DxMesh? lowMesh = ReferenceEquals(high, low) ? null : CreateDxMesh(low);
                cancellationToken.ThrowIfCancellationRequested();
                return (High: high, Low: low, HighMesh: highMesh, LowMesh: lowMesh,
                    Milliseconds: clock.Elapsed.TotalMilliseconds, Min: min > max ? 0 : min, Max: max);
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (isDisposed || version != loadVersion) throw new OperationCanceledException("A newer height field replaced this load.", cancellationToken);
            detailGeometry = result.High;
            interactionGeometry = result.Low;
            WorldWidth = worldWidth;
            WorldHeight = worldHeight;
            minimumGray = result.Min / 255;
            maximumGray = result.Max / 255;
            detailModel.Geometry = result.HighMesh;
            interactionModel.Geometry = result.LowMesh;
            SetInteracting(isInteracting);
            LastBuildMetrics = new HeightMapDxBuildMetrics(detailGeometry.Positions.Length, detailGeometry.Indices.Length / 3,
                interactionGeometry.Positions.Length, interactionGeometry.Indices.Length / 3, result.Milliseconds);
            return LastBuildMetrics;
        }

        private static DxMesh CreateDxMesh(HeightMapDxGeometry source) => new()
        {
            Positions = new Vector3Collection(source.Positions), Normals = new Vector3Collection(source.Normals),
            TextureCoordinates = new Vector2Collection(source.TextureCoordinates), Indices = new IntCollection(source.Indices)
        };

        public void SetInteracting(bool interacting)
        {
            isInteracting = interacting;
            bool low = interacting && interactionModel.Geometry != null;
            detailModel.IsRendering = detailModel.Geometry != null && !low;
            interactionModel.IsRendering = low;
        }

        public void SetHeightScale(double scale)
        {
            if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            heightScale = scale;
            heightTransform.ScaleZ = scale;
        }

        public void SetColormap(byte[]? bgrLut)
        {
            if (bgrLut != null && bgrLut.Length != 256 * 3) throw new ArgumentException("Expected a 256-entry BGR lookup table.", nameof(bgrLut));
            currentLut = bgrLut;
            if (bgrLut == null)
            {
                material.DiffuseMap = null;
                material.EmissiveMap = null;
                material.RenderDiffuseMap = false;
                material.RenderEmissiveMap = false;
                return;
            }
            var rgba = new byte[256 * 4];
            for (int i = 0; i < 256; i++)
            {
                rgba[i * 4] = bgrLut[i * 3 + 2];
                rgba[i * 4 + 1] = bgrLut[i * 3 + 1];
                rgba[i * 4 + 2] = bgrLut[i * 3];
                rgba[i * 4 + 3] = 255;
            }
            material.DiffuseMap = new TextureModel(rgba, SharpDX.DXGI.Format.R8G8B8A8_UNorm, 256, 1);
            material.EmissiveMap = material.DiffuseMap;
            material.RenderDiffuseMap = true;
            material.RenderEmissiveMap = true;
        }

        public void SetLighting(double ambient, double directional)
        {
            byte a = (byte)Math.Round(Math.Clamp(ambient, 0, 1) * 255);
            double intensity = Math.Clamp(directional, 0, 1);
            byte d = (byte)Math.Round(intensity * 255);
            ambientLight.Color = Color.FromRgb(a, a, a);
            // Phong ambient is added without multiplying the diffuse texture. Use the same LUT
            // as an emissive map to give the light slider an exact, unlit colormap endpoint.
            float baseColor = a / 255f;
            material.EmissiveColor = new Color4(baseColor, baseColor, baseColor, 1);
            keyLight.Color = Color.FromRgb(d, d, d);
            fillLight.Color = Color.FromRgb((byte)Math.Round(48 * intensity), (byte)Math.Round(56 * intensity), (byte)Math.Round(69 * intensity));
            float specular = (float)(0.13 * intensity);
            material.SpecularColor = new Color4(specular, specular, specular, 1);
        }

        public bool TryHit(Point viewportPoint, out HeightMapHit hit)
        {
            hit = default;
            if (detailGeometry == null || Viewport.ActualWidth < 1 || Viewport.ActualHeight < 1) return false;
            var ray = ViewportExtensions.UnProject(Viewport, viewportPoint);
            var active = isInteracting && interactionModel.Geometry != null ? interactionGeometry! : detailGeometry;
            if (!active.TryIntersect(ray.Position, ray.Direction, WorldWidth, WorldHeight, heightScale, out Vector3 position)) return false;
            int x = Math.Clamp((int)Math.Round(position.X / WorldWidth * (Sample.Width - 1)), 0, Sample.Width - 1);
            int y = Math.Clamp((int)Math.Round((WorldHeight - position.Y) / WorldHeight * (Sample.Height - 1)), 0, Sample.Height - 1);
            hit = new HeightMapHit(new Point3D(position.X, position.Y, position.Z), x, y, Sample.Gray[y * Sample.Width + x]);
            return true;
        }

        public BitmapSource? CaptureBitmap(int width, int height)
        {
            bool previous = isInteracting;
            try { SetInteracting(false); return ViewportExtensions.RenderBitmap(Viewport, width, height); }
            finally { SetInteracting(previous); }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            ++loadVersion;
            detailGeometry = null;
            interactionGeometry = null;
            currentLut = null;
            // A device-loss error in one cleanup step must not leave the remaining nodes,
            // render host, or effects manager alive, or throw out of Window.Closed.
            Release(() => detailModel.Geometry = null);
            Release(() => interactionModel.Geometry = null);
            Release(Viewport.Items.Clear);
            Release(detailModel.Dispose);
            Release(interactionModel.Dispose);
            Release(ambientLight.Dispose);
            Release(keyLight.Dispose);
            Release(fillLight.Dispose);
            Release(() => Viewport.EffectsManager = null);
            Release(Viewport.Dispose);
            Release(effectsManager.Dispose);
        }

        private static void Release(Action cleanup)
        {
            try { cleanup(); }
            catch (Exception ex) { Log.Warn("A height-map graphics resource could not be released cleanly.", ex); }
        }
    }
}
