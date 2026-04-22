using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    public partial class ModelViewer3DControl : UserControl
    {
        private sealed class GeometryMaterialState
        {
            public required GeometryModel3D Geometry { get; init; }
            public required Material Material { get; init; }
            public required Material BackMaterial { get; init; }
        }

        private HelixViewport3D? viewport;
        private ModelVisual3D? currentModelVisual;
        private Model3DGroup? currentModelGroup;
        private List<Visual3D>? axesVisuals;
        private List<GeometryMaterialState>? originalMaterialStates;
        private bool isWireframe;
        private bool isInitialized;
        private string? currentFilePath;

        private Point3D initialCameraPosition;
        private Vector3D initialLookDirection;
        private Vector3D initialUpDirection;

        public static ModelViewer3DConfig Config => ModelViewer3DConfig.Instance;

        public ModelViewer3DControl()
        {
            InitializeComponent();
            Loaded += ModelViewer3DControl_Loaded;
            Unloaded += ModelViewer3DControl_Unloaded;
        }

        private void ModelViewer3DControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (isInitialized) return;
            isInitialized = true;

            initialCameraPosition = new Point3D(180, -260, 180);
            initialLookDirection = new Vector3D(-180, 260, -160);
            initialUpDirection = new Vector3D(0, 0, 1);

            viewport = Viewport3DHelper.CreateDefaultViewport(
                initialCameraPosition,
                initialLookDirection,
                initialUpDirection,
                Config.FieldOfView);

            ContentGrid.Children.Add(viewport);

            axesVisuals = Viewport3DHelper.CreateFixedCornerAxes(20);
            foreach (var axis in axesVisuals)
                viewport.Children.Add(axis);

            CompositionTarget.Rendering += CompositionTarget_Rendering;

            if (Config.DefaultWireframe)
            {
                WireframeToggle.IsChecked = true;
                isWireframe = true;
            }

            if (!string.IsNullOrWhiteSpace(currentFilePath) && File.Exists(currentFilePath))
                _ = LoadModelAsync(currentFilePath);
        }

        private void ModelViewer3DControl_Unloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;

            if (viewport != null)
            {
                viewport.Children.Clear();
                ContentGrid.Children.Remove(viewport);
                viewport = null;
            }

            currentModelVisual = null;
            currentModelGroup = null;
            originalMaterialStates = null;
            axesVisuals = null;
            isInitialized = false;
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (viewport?.Camera is PerspectiveCamera camera && axesVisuals != null)
                Viewport3DHelper.UpdateFixedCornerAxes(axesVisuals, camera);
        }

        public void SetInitialFile(string? filePath)
        {
            currentFilePath = filePath;
        }

        public async Task InitializeAndLoadAsync(string filePath)
        {
            currentFilePath = filePath;

            if (!isInitialized)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    ModelViewer3DControl_Loaded(this, new RoutedEventArgs());
                }, DispatcherPriority.Loaded);
                return;
            }

            await LoadModelAsync(filePath);
        }

        public async Task LoadModelAsync(string filePath)
        {
            if (viewport == null) return;

            currentFilePath = filePath;
            Model3DGroup? modelGroup = null;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            int vertexCount = 0;
            int triangleCount = 0;
            Rect3D bounds = Rect3D.Empty;

            await Task.Run(() =>
            {
                if (ext == ".obj")
                {
                    bool deleteAfterLoad;
                    string objPath = PreprocessObjFileIfNeeded(filePath, out deleteAfterLoad);
                    try
                    {
                        var reader = new ObjReader();
                        modelGroup = reader.Read(objPath);
                    }
                    finally
                    {
                        if (deleteAfterLoad)
                        {
                            try { File.Delete(objPath); } catch { }
                        }
                    }
                }
                else if (ext == ".stl")
                {
                    var reader = new StLReader();
                    modelGroup = reader.Read(filePath);
                }

                if (modelGroup != null)
                {
                    CountGeometry(modelGroup, ref vertexCount, ref triangleCount);
                    bounds = Viewport3DHelper.GetBounds(modelGroup);
                    Viewport3DHelper.ApplyDefaultMaterial(modelGroup);
                }
            });

            if (modelGroup == null || !Viewport3DHelper.HasGeometry(modelGroup))
            {
                MessageBox.Show($"Failed to load model:\n{filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Viewport3DHelper.ClearLights(viewport);
            Viewport3DHelper.AddCameraAlignedLights(viewport, bounds);

            if (currentModelVisual != null)
                viewport.Children.Remove(currentModelVisual);

            currentModelGroup = modelGroup;
            originalMaterialStates = CaptureMaterialStates(modelGroup);

            currentModelVisual = new ModelVisual3D { Content = modelGroup };
            viewport.Children.Insert(0, currentModelVisual);

            if (viewport.Camera is PerspectiveCamera camera)
                Viewport3DHelper.FrameModel(camera, bounds);

            if (isWireframe)
                ApplyWireframe();

            Viewport3DHelper.TryZoomExtents(viewport);
            Dispatcher.BeginInvoke(() =>
            {
                if (viewport?.Camera is PerspectiveCamera delayedCamera)
                    Viewport3DHelper.FrameModel(delayedCamera, bounds);
            }, DispatcherPriority.Loaded);

            string fileName = Path.GetFileName(filePath);
            ModelInfoText.Text = $"{fileName}\nVertices: {vertexCount:N0}\nTriangles: {triangleCount:N0}";
        }

        private static void CountGeometry(Model3DGroup group, ref int vertexCount, ref int triangleCount)
        {
            foreach (var child in group.Children)
            {
                if (child is GeometryModel3D geom && geom.Geometry is MeshGeometry3D mesh)
                {
                    vertexCount += mesh.Positions.Count;
                    triangleCount += mesh.TriangleIndices.Count / 3;
                }
                else if (child is Model3DGroup childGroup)
                {
                    CountGeometry(childGroup, ref vertexCount, ref triangleCount);
                }
            }
        }

        private static List<GeometryMaterialState> CaptureMaterialStates(Model3DGroup group)
        {
            var states = new List<GeometryMaterialState>();
            CaptureMaterialStatesRecursive(group, states);
            return states;
        }

        private static void CaptureMaterialStatesRecursive(Model3D model, List<GeometryMaterialState> states)
        {
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    CaptureMaterialStatesRecursive(child, states);
                return;
            }

            if (model is GeometryModel3D geometry)
            {
                var material = geometry.Material ?? MaterialHelper.CreateMaterial(Brushes.LightGray);
                var backMaterial = geometry.BackMaterial ?? material;
                geometry.Material = material;
                geometry.BackMaterial = backMaterial;
                states.Add(new GeometryMaterialState
                {
                    Geometry = geometry,
                    Material = material,
                    BackMaterial = backMaterial,
                });
            }
        }

        private static string PreprocessObjFileIfNeeded(string originalPath, out bool deleteAfterLoad)
        {
            var lines = File.ReadAllLines(originalPath);
            bool hasInvalidMtllib = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("mtllib", StringComparison.OrdinalIgnoreCase))
                {
                    string afterTag = trimmed.Length > 6 ? trimmed.Substring(6).Trim() : string.Empty;
                    if (string.IsNullOrWhiteSpace(afterTag))
                    {
                        hasInvalidMtllib = true;
                        lines[i] = "# " + lines[i];
                    }
                }
            }

            if (!hasInvalidMtllib)
            {
                deleteAfterLoad = false;
                return originalPath;
            }

            string tempDirectory = Path.GetDirectoryName(originalPath) ?? Path.GetTempPath();
            string tempPath = Path.Combine(
                tempDirectory,
                $"{Path.GetFileNameWithoutExtension(originalPath)}.cv_fixed_{Guid.NewGuid():N}{Path.GetExtension(originalPath)}");
            File.WriteAllLines(tempPath, lines);
            deleteAfterLoad = true;
            return tempPath;
        }

        private void OpenModel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "3D Models (*.obj;*.stl)|*.obj;*.stl|OBJ Files (*.obj)|*.obj|STL Files (*.stl)|*.stl",
                DefaultExt = "obj",
                InitialDirectory = string.IsNullOrEmpty(Config.LastOpenDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : Config.LastOpenDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                Config.LastOpenDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
                _ = LoadModelAsync(dialog.FileName);
            }
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera is PerspectiveCamera camera && currentModelGroup != null)
            {
                var bounds = Viewport3DHelper.GetBounds(currentModelGroup);
                Viewport3DHelper.FrameModel(camera, bounds);
            }
            else if (viewport?.Camera is PerspectiveCamera fallbackCamera)
            {
                Viewport3DHelper.ResetCameraView(fallbackCamera, initialCameraPosition, initialLookDirection, initialUpDirection);
            }
        }

        private void Screenshot_Click(object sender, RoutedEventArgs e)
        {
            if (viewport != null)
                Viewport3DHelper.SaveScreenshot(viewport, $"Model3D_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        }

        private void ExportModel_Click(object sender, RoutedEventArgs e)
        {
            if (currentModelGroup == null) return;
            string defaultFileName = string.IsNullOrWhiteSpace(currentFilePath)
                ? $"Model3D_{DateTime.Now:yyyyMMdd_HHmmss}.obj"
                : Path.GetFileNameWithoutExtension(currentFilePath) + "_export.obj";
            Viewport3DHelper.ExportModel(currentModelGroup, defaultFileName);
        }

        private void WireframeToggle_Checked(object sender, RoutedEventArgs e)
        {
            isWireframe = true;
            ApplyWireframe();
        }

        private void WireframeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            isWireframe = false;
            RestoreSolid();
        }

        private void ApplyWireframe()
        {
            if (currentModelGroup == null) return;
            SetMaterialRecursive(currentModelGroup, MaterialHelper.CreateMaterial(Brushes.LimeGreen));
        }

        private void RestoreSolid()
        {
            if (originalMaterialStates == null) return;

            foreach (var state in originalMaterialStates)
            {
                state.Geometry.Material = state.Material;
                state.Geometry.BackMaterial = state.BackMaterial;
            }
        }

        private static void SetMaterialRecursive(Model3DGroup group, Material material)
        {
            foreach (var child in group.Children)
            {
                if (child is GeometryModel3D geom)
                {
                    geom.Material = material;
                    geom.BackMaterial = material;
                }
                else if (child is Model3DGroup childGroup)
                {
                    SetMaterialRecursive(childGroup, material);
                }
            }
        }
    }
}
