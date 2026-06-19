#pragma warning disable CS4014
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Data;

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

        private sealed class ModelNode
        {
            public required string Name { get; init; }
            public Model3D? Model { get; init; }
            public List<ModelNode> Children { get; } = new();
            public bool IsLeaf => Model is GeometryModel3D;
            public override string ToString() => Name;
        }

        private readonly Dictionary<Model3D, bool> modelVisibility = new();
        private readonly Dictionary<Model3D, ModelNode> nodeByModel = new();
        private ModelNode? selectedNode;

        private HelixViewport3D? viewport;
        private ModelVisual3D? currentModelVisual;
        private Model3DGroup? currentModelGroup;
        private List<Visual3D>? axesVisuals;
        private List<GeometryMaterialState>? originalMaterialStates;
        private bool isWireframe;
        private bool isInitialized;
        private bool hasLoadedModel;
        private string? currentFilePath;
        private bool keyboardHooked;

        // Object rotation tracking
        private Point lastMousePosition;
        private bool isLeftButtonDown;
        private Transform3DGroup currentTransform = new Transform3DGroup();
        private QuaternionRotation3D currentRotation = new QuaternionRotation3D();
        private Point3D rotationCenter;
        private bool hasRotationCenter;

        private Point3D initialCameraPosition;
        private Vector3D initialLookDirection;
        private Vector3D initialUpDirection;
        private string currentViewName = "ISO";
        private bool isOrthographic;

        public static ModelViewer3DConfig Config => ModelViewer3DConfig.Instance;

        public ModelViewer3DControl()
        {
            InitializeComponent();
            Loaded += ModelViewer3DControl_Loaded;
        }

        private void ModelViewer3DControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (isInitialized)
                return;

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

            viewport.MouseDown += Viewport_MouseDown;
            viewport.MouseMove += Viewport_MouseMove;
            viewport.MouseUp += Viewport_MouseUp;
            viewport.GotKeyboardFocus += Viewport_GotKeyboardFocus;
            viewport.MouseEnter += Viewport_MouseEnter;
            HookKeyboard();
            BindToolbarVisibility();
            Focus();
            Keyboard.Focus(this);

            axesVisuals = Viewport3DHelper.CreateFixedCornerAxes(20);
            foreach (var axis in axesVisuals)
                viewport.Children.Add(axis);

            CompositionTarget.Rendering += CompositionTarget_Rendering;

            WireframeToggle.IsChecked = Config.DefaultWireframe;
            TextureToggle.IsChecked = Config.IsTextureVisible;
            MaterialToggle.IsChecked = Config.IsMaterialVisible;
            ProjectionToggle.IsChecked = false;
            isWireframe = Config.DefaultWireframe;
            UpdateStatusBar();
            currentViewName = "ISO";

            if (!hasLoadedModel && !string.IsNullOrWhiteSpace(currentFilePath) && File.Exists(currentFilePath))
                LoadModelWithErrorHandling(currentFilePath);
        }

        public void DisposeViewer()
        {
            Loaded -= ModelViewer3DControl_Loaded;
            CompositionTarget.Rendering -= CompositionTarget_Rendering;

            if (viewport != null)
            {
                viewport.MouseDown -= Viewport_MouseDown;
                viewport.MouseMove -= Viewport_MouseMove;
                viewport.MouseUp -= Viewport_MouseUp;
                viewport.GotKeyboardFocus -= Viewport_GotKeyboardFocus;
                viewport.MouseEnter -= Viewport_MouseEnter;
                viewport.Children.Clear();
                ContentGrid.Children.Remove(viewport);
                viewport = null;
            }

            // Release 3D model geometry and material resources
            if (currentModelGroup != null)
            {
                ReleaseModelResources(currentModelGroup);
                currentModelGroup = null;
            }

            UnhookKeyboard();
            currentModelVisual = null;
            originalMaterialStates = null;
            axesVisuals = null;
            modelVisibility.Clear();
            nodeByModel.Clear();
            selectedNode = null;
            ModelTreeView.ItemsSource = null;
            isInitialized = false;
            hasLoadedModel = false;

            // Force GC to release WPF 3D unmanaged rendering resources
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
        }

        private static void ReleaseModelResources(Model3D model)
        {
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    ReleaseModelResources(child);
                group.Children.Clear();
                return;
            }

            if (model is GeometryModel3D geometry)
            {
                // Clear mesh data to free vertex/index buffers
                if (geometry.Geometry is MeshGeometry3D mesh)
                {
                    mesh.Positions = null;
                    mesh.TriangleIndices = null;
                    mesh.TextureCoordinates = null;
                    mesh.Normals = null;
                }
                geometry.Geometry = null;

                // Release material textures
                ReleaseMaterial(geometry.Material);
                ReleaseMaterial(geometry.BackMaterial);
                geometry.Material = null;
                geometry.BackMaterial = null;
            }
        }

        private static void ReleaseMaterial(Material? material)
        {
            if (material is MaterialGroup group)
            {
                foreach (var child in group.Children)
                    ReleaseMaterial(child);
                group.Children.Clear();
                return;
            }

            if (material is DiffuseMaterial diffuse && diffuse.Brush is ImageBrush imageBrush)
                imageBrush.ImageSource = null;
            else if (material is EmissiveMaterial emissive && emissive.Brush is ImageBrush emissiveBrush)
                emissiveBrush.ImageSource = null;
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (viewport?.Camera is ProjectionCamera camera && axesVisuals != null)
                Viewport3DHelper.UpdateFixedCornerAxes(axesVisuals, camera);
        }

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            Keyboard.Focus(this);
            if (e.ChangedButton != MouseButton.Left || currentModelVisual == null) return;
            isLeftButtonDown = true;
            lastMousePosition = e.GetPosition(viewport);
            viewport?.CaptureMouse();
            e.Handled = true;
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isLeftButtonDown || currentModelVisual == null || viewport == null) return;

            Point currentPosition = e.GetPosition(viewport);
            Vector delta = currentPosition - lastMousePosition;
            lastMousePosition = currentPosition;

            const double rotationSpeed = 0.45;
            Quaternion yaw = new Quaternion(new Vector3D(0, 0, 1), delta.X * rotationSpeed);
            Quaternion pitch = new Quaternion(new Vector3D(1, 0, 0), delta.Y * rotationSpeed);
            currentRotation.Quaternion = yaw * pitch * currentRotation.Quaternion;
            currentModelVisual.Transform = currentTransform;
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            isLeftButtonDown = false;
            viewport?.ReleaseMouseCapture();
            e.Handled = true;
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
            hasLoadedModel = false;
            ShowLoading(true, $"Loading {Path.GetFileName(filePath)}...");

            Model3DGroup? modelGroup = null;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            int vertexCount = 0;
            int triangleCount = 0;
            Rect3D bounds = Rect3D.Empty;

            try
            {
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
                        PrepareModelForDisplay(modelGroup, ext == ".obj");
                        CountGeometry(modelGroup, ref vertexCount, ref triangleCount);
                        bounds = Viewport3DHelper.GetBounds(modelGroup);
                        Viewport3DHelper.ApplyDefaultMaterial(modelGroup);
                        FreezeModelForTransfer(modelGroup);
                    }
                });

                if (modelGroup == null || !Viewport3DHelper.HasGeometry(modelGroup))
                {
                    MessageBox.Show($"Failed to load model:\n{filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!modelGroup.IsFrozen)
                    throw new InvalidOperationException("The loaded model could not be transferred to the UI thread.");

                modelGroup = modelGroup.Clone();

                if (currentModelVisual != null)
                    viewport.Children.Remove(currentModelVisual);

                currentModelGroup = modelGroup;
                originalMaterialStates = CaptureMaterialStates(modelGroup);
                UpdateRotationCenterFromBounds(bounds);
                ResetModelRotation();

                currentModelVisual = new ModelVisual3D { Content = modelGroup, Transform = currentTransform };
                viewport.Children.Insert(0, currentModelVisual);

                if (viewport.Camera is PerspectiveCamera perspCamera)
                {
                    Viewport3DHelper.FrameModel(perspCamera, bounds);
                }
                else if (viewport.Camera is OrthographicCamera orthoCamera)
                {
                    FrameCameraToModel(orthoCamera, bounds);
                }

                Viewport3DHelper.ClearLights(viewport);
                Viewport3DHelper.AddCameraAlignedLights(viewport, bounds);

                if (isWireframe)
                    ApplyWireframe();

                Viewport3DHelper.TryZoomExtents(viewport);
                Dispatcher.BeginInvoke(() =>
                {
                    if (viewport?.Camera is PerspectiveCamera delayedPersp)
                    {
                        Viewport3DHelper.FrameModel(delayedPersp, bounds);
                    }
                    else if (viewport?.Camera is OrthographicCamera delayedOrtho)
                    {
                        FrameCameraToModel(delayedOrtho, bounds);
                    }

                    if (viewport != null)
                    {
                        Viewport3DHelper.ClearLights(viewport);
                        Viewport3DHelper.AddCameraAlignedLights(viewport, bounds);
                    }
                }, DispatcherPriority.Loaded);

                BuildModelTree();
                ApplyVisibilityState();
                hasLoadedModel = true;

                string fileName = Path.GetFileName(filePath);
                ModelInfoText.Text = $"{fileName}\nVertices: {vertexCount:N0}\nTriangles: {triangleCount:N0}";
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async void LoadModelWithErrorHandling(string filePath)
        {
            try
            {
                await LoadModelAsync(filePath);
            }
            catch (Exception ex)
            {
                string fileName = Path.GetFileName(filePath);
                ModelInfoText.Text = $"{fileName}\nLoad failed";
                MessageBox.Show($"Failed to load model:\n{filePath}\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private static void PrepareModelForDisplay(Model3DGroup group, bool preferVisibleFallback)
        {
            bool forceVisibleMaterial = preferVisibleFallback && !HasVisibleMaterial(group);
            PrepareModelForDisplayRecursive(group, forceVisibleMaterial);
        }

        private static void PrepareModelForDisplayRecursive(Model3D model, bool forceVisibleMaterial)
        {
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    PrepareModelForDisplayRecursive(child, forceVisibleMaterial);
                return;
            }

            if (model is not GeometryModel3D geometry)
                return;

            if (geometry.Geometry is MeshGeometry3D mesh)
                EnsureMeshNormals(mesh);

            Material fallbackMaterial = MaterialHelper.CreateMaterial(Brushes.White);
            if (geometry.Material == null || forceVisibleMaterial && MaterialLikelyInvisible(geometry.Material))
                geometry.Material = fallbackMaterial;

            if (geometry.BackMaterial == null || forceVisibleMaterial && MaterialLikelyInvisible(geometry.BackMaterial))
                geometry.BackMaterial = geometry.Material;
        }

        private static bool HasVisibleMaterial(Model3D model)
        {
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (HasVisibleMaterial(child))
                        return true;
                }
                return false;
            }

            if (model is not GeometryModel3D geometry)
                return false;

            return !MaterialLikelyInvisible(geometry.Material) || !MaterialLikelyInvisible(geometry.BackMaterial);
        }

        private static bool MaterialLikelyInvisible(Material? material)
        {
            if (material == null)
                return true;

            if (material is MaterialGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (!MaterialLikelyInvisible(child))
                        return false;
                }
                return true;
            }

            if (material is DiffuseMaterial diffuse)
                return BrushLikelyInvisible(diffuse.Brush);

            if (material is EmissiveMaterial emissive)
                return BrushLikelyInvisible(emissive.Brush);

            return true;
        }

        private static bool BrushLikelyInvisible(Brush? brush)
        {
            if (brush == null || brush.Opacity <= 0.01)
                return true;

            if (brush is ImageBrush imageBrush)
                return imageBrush.ImageSource == null;

            if (brush is SolidColorBrush solidColorBrush)
            {
                var color = solidColorBrush.Color;
                int maxChannel = Math.Max(color.R, Math.Max(color.G, color.B));
                return color.A <= 8 || maxChannel <= 12;
            }

            return false;
        }

        private static void EnsureMeshNormals(MeshGeometry3D mesh)
        {
            if (mesh.Positions.Count == 0)
                return;

            if (mesh.Normals != null && mesh.Normals.Count == mesh.Positions.Count && HasUsableNormals(mesh.Normals))
                return;

            var normals = new Vector3D[mesh.Positions.Count];
            for (int i = 0; i + 2 < mesh.TriangleIndices.Count; i += 3)
            {
                int index0 = mesh.TriangleIndices[i];
                int index1 = mesh.TriangleIndices[i + 1];
                int index2 = mesh.TriangleIndices[i + 2];

                if (index0 < 0 || index1 < 0 || index2 < 0 ||
                    index0 >= mesh.Positions.Count || index1 >= mesh.Positions.Count || index2 >= mesh.Positions.Count)
                {
                    continue;
                }

                Vector3D edge1 = mesh.Positions[index1] - mesh.Positions[index0];
                Vector3D edge2 = mesh.Positions[index2] - mesh.Positions[index0];
                Vector3D faceNormal = Vector3D.CrossProduct(edge1, edge2);
                if (faceNormal.LengthSquared <= 1e-12)
                    continue;

                faceNormal.Normalize();
                normals[index0] += faceNormal;
                normals[index1] += faceNormal;
                normals[index2] += faceNormal;
            }

            var normalCollection = new Vector3DCollection(mesh.Positions.Count);
            for (int i = 0; i < normals.Length; i++)
            {
                Vector3D normal = normals[i];
                if (normal.LengthSquared <= 1e-12)
                    normal = new Vector3D(0, 0, 1);
                else
                    normal.Normalize();

                normalCollection.Add(normal);
            }

            mesh.Normals = normalCollection;
        }

        private static bool HasUsableNormals(Vector3DCollection normals)
        {
            foreach (var normal in normals)
            {
                if (normal.LengthSquared > 1e-12)
                    return true;
            }

            return false;
        }

        private static void FreezeModelForTransfer(Model3D model)
        {
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    FreezeModelForTransfer(child);
            }
            else if (model is GeometryModel3D geometry)
            {
                FreezeFreezable(geometry.Geometry);
                FreezeMaterialForTransfer(geometry.Material);
                FreezeMaterialForTransfer(geometry.BackMaterial);
            }

            FreezeFreezable(model);
        }

        private static void FreezeMaterialForTransfer(Material? material)
        {
            if (material is MaterialGroup group)
            {
                foreach (var child in group.Children)
                    FreezeMaterialForTransfer(child);
            }
            else if (material is DiffuseMaterial diffuse)
            {
                FreezeBrushForTransfer(diffuse.Brush);
            }
            else if (material is EmissiveMaterial emissive)
            {
                FreezeBrushForTransfer(emissive.Brush);
            }
            else if (material is SpecularMaterial specular)
            {
                FreezeBrushForTransfer(specular.Brush);
            }

            FreezeFreezable(material);
        }

        private static void FreezeBrushForTransfer(Brush? brush)
        {
            if (brush is ImageBrush imageBrush)
                FreezeFreezable(imageBrush.ImageSource as Freezable);

            FreezeFreezable(brush);
        }

        private static void FreezeFreezable(Freezable? freezable)
        {
            if (freezable?.CanFreeze == true && !freezable.IsFrozen)
                freezable.Freeze();
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
                var material = geometry.Material ?? MaterialHelper.CreateMaterial(Brushes.White);
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
                LoadModelWithErrorHandling(dialog.FileName);
            }
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            if (currentModelGroup != null)
            {
                var bounds = Viewport3DHelper.GetBounds(currentModelGroup);
                UpdateRotationCenterFromBounds(bounds);
            }
            ResetModelRotation();

            if (viewport?.Camera is ProjectionCamera camera)
            {
                if (currentModelGroup != null)
                {
                    var bounds = Viewport3DHelper.GetBounds(currentModelGroup);
                    if (!bounds.IsEmpty)
                        FrameCameraToModel(camera, bounds);
                }
                else
                {
                    camera.Position = initialCameraPosition;
                    camera.LookDirection = initialLookDirection;
                    camera.UpDirection = initialUpDirection;
                }
            }

            currentViewName = "ISO";
            UpdateStatusBar();
        }

        private static void FrameCameraToModel(ProjectionCamera camera, Rect3D bounds)
        {
            var center = new Point3D(
                bounds.X + bounds.SizeX / 2,
                bounds.Y + bounds.SizeY / 2,
                bounds.Z + bounds.SizeZ / 2);

            double radius = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
            if (radius <= 0) radius = 1;

            double distance = radius * 3.2;
            camera.Position = new Point3D(
                center.X + distance * 0.42,
                center.Y - distance,
                center.Z + distance * 0.78);
            camera.LookDirection = center - camera.Position;
            camera.UpDirection = new Vector3D(0, 0, 1);
            camera.NearPlaneDistance = Math.Max(distance / 500, 0.1);
            camera.FarPlaneDistance = Math.Max(distance * 120, 10000);

            if (camera is OrthographicCamera ortho)
                ortho.Width = radius * 2.5;
        }

        private void CameraMoveLeft_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera is not ProjectionCamera camera) return;
            camera.Position = new Point3D(camera.Position.X - 20, camera.Position.Y, camera.Position.Z);
            UpdateStatusBar();
        }

        private void CameraMoveForward_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera is not ProjectionCamera camera) return;
            camera.Position = new Point3D(camera.Position.X, camera.Position.Y + 20, camera.Position.Z);
            UpdateStatusBar();
        }

        private void CameraMoveRight_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera is not ProjectionCamera camera) return;
            camera.Position = new Point3D(camera.Position.X + 20, camera.Position.Y, camera.Position.Z);
            UpdateStatusBar();
        }

        private void CameraMoveBack_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera is not ProjectionCamera camera) return;
            camera.Position = new Point3D(camera.Position.X, camera.Position.Y - 20, camera.Position.Z);
            UpdateStatusBar();
        }

        private void LookLeft_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera is not ProjectionCamera camera) return;
            camera.LookDirection = new Vector3D(camera.LookDirection.X - 10, camera.LookDirection.Y, camera.LookDirection.Z);
            UpdateStatusBar();
        }

        private void LookUp_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera is not ProjectionCamera camera) return;
            camera.LookDirection = new Vector3D(camera.LookDirection.X, camera.LookDirection.Y, camera.LookDirection.Z + 10);
            UpdateStatusBar();
        }

        private void LookRight_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera is not ProjectionCamera camera) return;
            camera.LookDirection = new Vector3D(camera.LookDirection.X + 10, camera.LookDirection.Y, camera.LookDirection.Z);
            UpdateStatusBar();
        }

        private void LookDown_Click(object sender, RoutedEventArgs e)
        {
            if (viewport?.Camera is not ProjectionCamera camera) return;
            camera.LookDirection = new Vector3D(camera.LookDirection.X, camera.LookDirection.Y, camera.LookDirection.Z - 10);
            UpdateStatusBar();
        }

        private void FrontView_Click(object sender, RoutedEventArgs e) => ApplyPresetView(Properties.Resources.ThreeD_Front, new Vector3D(0, -1, 0), new Vector3D(0, 0, 1));
        private void BackView_Click(object sender, RoutedEventArgs e) => ApplyPresetView(Properties.Resources.ThreeD_Back, new Vector3D(0, 1, 0), new Vector3D(0, 0, 1));
        private void LeftView_Click(object sender, RoutedEventArgs e) => ApplyPresetView(Properties.Resources.ThreeD_Left, new Vector3D(1, 0, 0), new Vector3D(0, 0, 1));
        private void RightView_Click(object sender, RoutedEventArgs e) => ApplyPresetView(Properties.Resources.ThreeD_Right, new Vector3D(-1, 0, 0), new Vector3D(0, 0, 1));
        private void TopView_Click(object sender, RoutedEventArgs e) => ApplyPresetView(Properties.Resources.ThreeD_Top, new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));
        private void BottomView_Click(object sender, RoutedEventArgs e) => ApplyPresetView(Properties.Resources.ThreeD_Bottom, new Vector3D(0, 0, 1), new Vector3D(0, -1, 0));
        private void IsoView_Click(object sender, RoutedEventArgs e) => ResetView_Click(sender, e);

        private void ProjectionToggle_Checked(object sender, RoutedEventArgs e)
        {
            isOrthographic = true;
            SwitchCamera(isOrthographic: true);
            UpdateStatusBar();
        }

        private void ProjectionToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            isOrthographic = false;
            SwitchCamera(isOrthographic: false);
            UpdateStatusBar();
        }

        private void SwitchCamera(bool isOrthographic)
        {
            if (viewport == null) return;

            var oldCamera = viewport.Camera as ProjectionCamera;
            if (oldCamera == null) return;

            double width = 100;
            if (oldCamera is PerspectiveCamera persp)
                width = persp.FieldOfView;
            else if (oldCamera is OrthographicCamera ortho)
                width = ortho.Width;

            ProjectionCamera newCamera;
            if (isOrthographic)
            {
                var perspOld = oldCamera as PerspectiveCamera;
                double dist = perspOld?.LookDirection.Length ?? 500;
                double orthoWidth = dist * 2 * Math.Tan((perspOld?.FieldOfView ?? 60) * Math.PI / 360.0);
                newCamera = new OrthographicCamera
                {
                    Position = oldCamera.Position,
                    LookDirection = oldCamera.LookDirection,
                    UpDirection = oldCamera.UpDirection,
                    Width = orthoWidth,
                    NearPlaneDistance = oldCamera.NearPlaneDistance,
                    FarPlaneDistance = oldCamera.FarPlaneDistance,
                };
            }
            else
            {
                var orthoOld = oldCamera as OrthographicCamera;
                double dist = oldCamera.LookDirection.Length;
                if (dist < 1) dist = 500;
                double fov = 2 * Math.Atan2(orthoOld?.Width ?? 100, 2 * dist) * 180.0 / Math.PI;
                newCamera = new PerspectiveCamera
                {
                    Position = oldCamera.Position,
                    LookDirection = oldCamera.LookDirection,
                    UpDirection = oldCamera.UpDirection,
                    FieldOfView = fov,
                    NearPlaneDistance = oldCamera.NearPlaneDistance,
                    FarPlaneDistance = oldCamera.FarPlaneDistance,
                };
            }

            viewport.Camera = newCamera;

            Viewport3DHelper.ClearLights(viewport);
            if (currentModelGroup != null)
            {
                var bounds = Viewport3DHelper.GetBounds(currentModelGroup);
                Viewport3DHelper.AddCameraAlignedLights(viewport, bounds);
            }
            else
            {
                Viewport3DHelper.AddCameraAlignedLights(viewport, Rect3D.Empty);
            }
        }

        private void ApplyPresetView(string viewName, Vector3D direction, Vector3D upDirection)
        {
            if (viewport?.Camera is not ProjectionCamera camera || currentModelGroup == null)
                return;

            Rect3D bounds = Viewport3DHelper.GetBounds(currentModelGroup);
            if (bounds.IsEmpty)
                return;

            Point3D center = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
            double radius = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
            if (radius <= 0)
                radius = 1;

            direction.Normalize();
            camera.Position = center - direction * (radius * 2.6);
            camera.LookDirection = center - camera.Position;
            camera.UpDirection = upDirection;

            if (camera is OrthographicCamera ortho)
                ortho.Width = radius * 2.5;

            currentViewName = viewName;
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            StatusProjectionText.Text = $"{Properties.Resources.ThreeD_Projection}: {(isOrthographic ? Properties.Resources.ThreeD_Orthographic : Properties.Resources.ThreeD_Perspective)}";
            StatusViewText.Text = $"{Properties.Resources.ThreeD_View}: {currentViewName}";

            if (viewport?.Camera is ProjectionCamera camera)
            {
                StatusCameraText.Text = $"{Properties.Resources.ThreeD_Camera}: X {camera.Position.X:F0}  Y {camera.Position.Y:F0}  Z {camera.Position.Z:F0}";
            }
            else
            {
                StatusCameraText.Text = $"{Properties.Resources.ThreeD_Camera}: -";
            }
        }

        private void ShowLoading(bool show, string? message = null)
        {
            LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (message != null)
                LoadingText.Text = message;
        }

        private void Viewport_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Keyboard.Focus(this);
        }

        private void Viewport_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsKeyboardFocusWithin)
                Keyboard.Focus(this);
        }

        private void HookKeyboard()
        {
            if (keyboardHooked)
                return;

            PreviewKeyDown += ModelViewer3DControl_PreviewKeyDown;
            keyboardHooked = true;
        }

        private void UnhookKeyboard()
        {
            if (!keyboardHooked)
                return;

            PreviewKeyDown -= ModelViewer3DControl_PreviewKeyDown;
            keyboardHooked = false;
        }

        private void BindToolbarVisibility()
        {
            var converter = Application.Current.TryFindResource("bool2VisibilityConverter") as IValueConverter;
            ToolbarPanel.SetBinding(VisibilityProperty, new Binding(nameof(ModelViewer3DConfig.IsToolbarVisible))
            {
                Source = Config,
                Converter = converter
            });
        }

        private void ModelViewer3DControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsKeyboardFocusWithin)
                return;

            if (e.OriginalSource is DependencyObject source)
            {
                var focusedElement = Keyboard.FocusedElement as DependencyObject;
                if (focusedElement != null && focusedElement != this && !IsDescendantOf(focusedElement, this))
                    return;
            }

            if (viewport?.Camera is not ProjectionCamera camera)
                return;

            switch (e.Key)
            {
                case Key.Home:
                    ResetView_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.H:
                    Config.IsToolbarVisible = !Config.IsToolbarVisible;
                    e.Handled = true;
                    break;
                default:
                    e.Handled = Viewport3DHelper.HandleCameraKey(camera, e.Key);
                    break;
            }

            if (e.Handled)
                UpdateStatusBar();
        }


        private void BuildModelTree()
        {
            nodeByModel.Clear();
            modelVisibility.Clear();

            if (currentModelGroup == null)
            {
                ModelTreeView.ItemsSource = null;
                return;
            }

            var root = CreateModelNode(currentModelGroup, "Root");
            ModelTreeView.ItemsSource = new[] { root };
        }

        private ModelNode CreateModelNode(Model3D model, string fallbackName)
        {
            string name = fallbackName;
            if (model is GeometryModel3D geometry)
                name = geometry.GetHashCode().ToString("X");

            var node = new ModelNode { Name = name, Model = model };
            nodeByModel[model] = node;
            modelVisibility[model] = true;

            if (model is Model3DGroup group)
            {
                for (int i = 0; i < group.Children.Count; i++)
                    node.Children.Add(CreateModelNode(group.Children[i], $"Node_{i + 1}"));
            }

            return node;
        }

        private void ModelTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            selectedNode = e.NewValue as ModelNode;
        }

        private void IsolateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (currentModelGroup == null || selectedNode?.Model == null)
                return;

            foreach (var key in nodeByModel.Keys.ToList())
                modelVisibility[key] = false;

            SetVisibilityRecursive(selectedNode.Model, true);
            ApplyModelVisibility();
        }

        private void ShowAllNodes_Click(object sender, RoutedEventArgs e)
        {
            foreach (var key in nodeByModel.Keys.ToList())
                modelVisibility[key] = true;

            ApplyModelVisibility();
        }

        private void SetVisibilityRecursive(Model3D model, bool visible)
        {
            modelVisibility[model] = visible;
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    SetVisibilityRecursive(child, visible);
            }
        }

        private void ApplyModelVisibility()
        {
            if (currentModelGroup == null)
                return;

            ApplyModelVisibilityRecursive(currentModelGroup);
            ApplyVisibilityState();
        }

        private void ApplyModelVisibilityRecursive(Model3D model)
        {
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    ApplyModelVisibilityRecursive(child);
                return;
            }

            if (model is GeometryModel3D geometry)
            {
                bool visible = modelVisibility.TryGetValue(model, out bool isVisible) ? isVisible : true;
                if (!visible)
                {
                    geometry.Material = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)));
                    geometry.BackMaterial = geometry.Material;
                }
            }
        }

        private static bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            DependencyObject? current = child;
            while (current != null)
            {
                if (current == parent)
                    return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void UpdateRotationCenterFromBounds(Rect3D bounds)
        {
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
            if (currentModelVisual != null)
                currentModelVisual.Transform = currentTransform;
        }

        private void ApplyVisibilityState()
        {
            if (currentModelGroup == null || originalMaterialStates == null)
                return;

            if (isWireframe)
            {
                // Wireframe overlay handles rendering — make original model nearly invisible
                SetMaterialRecursive(currentModelGroup, new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))));
                return;
            }

            if (!Config.IsMaterialVisible)
            {
                var hiddenMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(8, 180, 180, 180)));
                SetMaterialRecursive(currentModelGroup, hiddenMaterial);
                return;
            }

            foreach (var state in originalMaterialStates)
            {
                state.Geometry.Material = Config.IsTextureVisible ? state.Material : RemoveTextureFromMaterial(state.Material);
                state.Geometry.BackMaterial = Config.IsTextureVisible ? state.BackMaterial : RemoveTextureFromMaterial(state.BackMaterial);
            }
        }

        private static Material RemoveTextureFromMaterial(Material material)
        {
            if (material is MaterialGroup group)
            {
                var newGroup = new MaterialGroup();
                foreach (var child in group.Children)
                    newGroup.Children.Add(RemoveTextureFromMaterial(child));
                return newGroup;
            }

            if (material is DiffuseMaterial diffuse)
                return diffuse.Brush is ImageBrush ? new DiffuseMaterial(Brushes.White) : diffuse.Clone();

            if (material is EmissiveMaterial emissive)
                return emissive.Brush is ImageBrush ? new EmissiveMaterial(Brushes.White) : emissive.Clone();

            if (material is SpecularMaterial specular)
                return specular.Brush is ImageBrush ? new SpecularMaterial(Brushes.White, specular.SpecularPower) : specular.Clone();

            return material.Clone();
        }

        private void TextureToggle_Checked(object sender, RoutedEventArgs e)
        {
            Config.IsTextureVisible = true;
            if (!Config.IsMaterialVisible)
            {
                Config.IsMaterialVisible = true;
                MaterialToggle.IsChecked = true;
            }
            ApplyVisibilityState();
        }

        private void TextureToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            Config.IsTextureVisible = false;
            ApplyVisibilityState();
        }

        private void MaterialToggle_Checked(object sender, RoutedEventArgs e)
        {
            Config.IsMaterialVisible = true;
            ApplyVisibilityState();
        }

        private void MaterialToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            Config.IsMaterialVisible = false;
            if (Config.IsTextureVisible)
            {
                Config.IsTextureVisible = false;
                TextureToggle.IsChecked = false;
            }
            ApplyVisibilityState();
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
            Config.DefaultWireframe = true;
            ApplyWireframe();
        }

        private void WireframeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            isWireframe = false;
            Config.DefaultWireframe = false;
            RestoreSolid();
        }

        private void ApplyWireframe()
        {
            if (currentModelGroup == null || viewport == null) return;

            // Hide original model by making it transparent
            SetMaterialRecursive(currentModelGroup, new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))));

            // Create real wireframe geometry
            var wireframe = Viewport3DHelper.CreateWireframeGeometry(currentModelGroup);
            if (wireframe != null)
            {
                var wireframeVisual = new ModelVisual3D { Content = wireframe };
                wireframeVisual.SetValue(WireframeVisualTagProperty, true);
                viewport.Children.Add(wireframeVisual);
            }
        }

        private void RestoreSolid()
        {
            if (viewport == null) return;

            // Remove wireframe visuals
            for (int i = viewport.Children.Count - 1; i >= 0; i--)
            {
                if (viewport.Children[i] is ModelVisual3D mv && mv.GetValue(WireframeVisualTagProperty) is true)
                    viewport.Children.RemoveAt(i);
            }

            // Restore original materials
            ApplyVisibilityState();
        }

        private static readonly DependencyProperty WireframeVisualTagProperty =
            DependencyProperty.RegisterAttached("WireframeVisualTag", typeof(bool), typeof(ModelViewer3DControl), new PropertyMetadata(false));

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
