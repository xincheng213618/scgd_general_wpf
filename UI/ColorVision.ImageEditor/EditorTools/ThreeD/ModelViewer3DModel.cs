using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Assimp;
using HelixToolkit.SharpDX.Model;
using HelixToolkit.SharpDX.Model.Scene;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    internal sealed record ModelViewer3DStatistics(
        long FileSize,
        int NodeCount,
        int MeshCount,
        long VertexCount,
        long TriangleCount,
        int MaterialCount,
        int TextureCount,
        IReadOnlyList<string> MissingTexturePaths,
        BoundingBox Bounds,
        bool HasBounds,
        bool SuggestedVerticalFlip,
        TimeSpan LoadDuration)
    {
        public float Width => HasBounds ? Bounds.Maximum.X - Bounds.Minimum.X : 0;
        public float Depth => HasBounds ? Bounds.Maximum.Y - Bounds.Minimum.Y : 0;
        public float Height => HasBounds ? Bounds.Maximum.Z - Bounds.Minimum.Z : 0;
    }

    internal sealed class ModelViewerSceneItem : INotifyPropertyChanged
    {
        private bool isSelected;
        private bool isExpanded;
        private bool isEffectivelyVisible = true;
        private bool isVisibleInFilter = true;

        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required SceneNode Node { get; init; }
        public required bool IsMesh { get; init; }
        public ModelViewerSceneItem? Parent { get; init; }
        public ObservableCollection<ModelViewerSceneItem> Children { get; } = new();
        public long VertexCount { get; init; }
        public long TriangleCount { get; init; }
        public string MaterialName { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        public bool IsExpanded
        {
            get => isExpanded;
            set => SetProperty(ref isExpanded, value);
        }

        public bool IsEffectivelyVisible
        {
            get => isEffectivelyVisible;
            internal set => SetProperty(ref isEffectivelyVisible, value);
        }

        public bool IsVisibleInFilter
        {
            get => isVisibleInFilter;
            internal set => SetProperty(ref isVisibleInFilter, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public IEnumerable<ModelViewerSceneItem> SelfAndDescendants()
        {
            yield return this;
            foreach (ModelViewerSceneItem child in Children)
            {
                foreach (ModelViewerSceneItem descendant in child.SelfAndDescendants())
                    yield return descendant;
            }
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal sealed class ModelViewer3DModel : IDisposable
    {
        private static readonly Color4 WireframeColor = new(0.22f, 0.62f, 0.96f, 1f);
        private readonly Dictionary<MeshNode, MaterialCore?> originalMaterials;
        private readonly Dictionary<SceneNode, ModelViewerSceneItem> itemByNode;
        private readonly DiffuseMaterialCore solidMaterial = new()
        {
            Name = "ColorVision Solid",
            DiffuseColor = new Color4(0.54f, 0.59f, 0.66f, 1f),
        };
        private readonly List<MeshNode> selectedMeshes = new();
        private bool isDisposed;

        public ModelViewer3DModel(string filePath, HelixToolkitScene scene, TimeSpan loadDuration, CancellationToken cancellationToken)
        {
            FilePath = filePath;
            Scene = scene;
            Root = scene.Root ?? throw new ArgumentException("The imported scene has no root node.", nameof(scene));

            cancellationToken.ThrowIfCancellationRequested();
            Root.UpdateAllTransformMatrix();
            SceneNode[] nodes = Root.Traverse().ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            Meshes = nodes.OfType<MeshNode>().ToArray();
            originalMaterials = Meshes.ToDictionary(mesh => mesh, mesh => mesh.Material);
            itemByNode = new Dictionary<SceneNode, ModelViewerSceneItem>();

            string fallbackName = Path.GetFileName(filePath);
            ModelViewerSceneItem rootItem = BuildTree(Root, null, fallbackName, cancellationToken);
            SceneItems = new ObservableCollection<ModelViewerSceneItem> { rootItem };

            long vertices = 0;
            long triangles = 0;
            foreach (MeshNode mesh in Meshes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (mesh.Geometry is MeshGeometry3D geometry)
                {
                    vertices += geometry.Positions?.Count ?? 0;
                    triangles += (geometry.Indices?.Count ?? 0) / 3;
                }
            }

            List<string> texturePaths = Meshes
                .Select(mesh => GetTexturePath(mesh.Material))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => ResolveTexturePath(filePath, path!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            cancellationToken.ThrowIfCancellationRequested();
            List<string> missingTextures = texturePaths
                .Where(path => !path.StartsWith('*') && !File.Exists(path))
                .ToList();
            int materialCount = Meshes
                .Select(mesh => mesh.Material)
                .Where(material => material != null)
                .Distinct(ReferenceEqualityComparer.Instance)
                .Count();

            bool hasBounds = Root.TryGetBound(out BoundingBox bounds) && IsFinite(bounds);
            bool suggestedVerticalFlip = hasBounds && SuggestVerticalFlip(Meshes, bounds, cancellationToken);
            Statistics = new ModelViewer3DStatistics(
                new FileInfo(filePath).Length,
                nodes.Length,
                Meshes.Count,
                vertices,
                triangles,
                materialCount,
                texturePaths.Count,
                missingTextures,
                bounds,
                hasBounds,
                suggestedVerticalFlip,
                loadDuration);
        }

        public string FilePath { get; }
        public HelixToolkitScene Scene { get; }
        public SceneNode Root { get; }
        public IReadOnlyList<MeshNode> Meshes { get; }
        public ObservableCollection<ModelViewerSceneItem> SceneItems { get; }
        public ModelViewer3DStatistics Statistics { get; }

        public ModelViewerSceneItem? FindItem(SceneNode node)
        {
            itemByNode.TryGetValue(node, out ModelViewerSceneItem? item);
            return item;
        }

        public void ApplyRenderMode(ModelViewerRenderMode mode)
        {
            foreach (MeshNode mesh in Meshes)
            {
                mesh.RenderWireframe = mode == ModelViewerRenderMode.Wireframe;
                mesh.WireframeColor = WireframeColor;
                mesh.Material = mode == ModelViewerRenderMode.Solid ? solidMaterial : originalMaterials[mesh];
            }
        }

        public ErrorCode ExportToFile(string filePath, string formatId, ModelViewerRenderMode displayMode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(formatId);

            return WithOriginalMaterials(displayMode, scene =>
            {
                using Exporter exporter = new();
                return exporter.ExportToFile(filePath, scene, formatId);
            });
        }

        public TResult WithOriginalMaterials<TResult>(ModelViewerRenderMode displayMode, Func<HelixToolkitScene, TResult> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            foreach (MeshNode mesh in Meshes)
            {
                mesh.RenderWireframe = false;
                mesh.Material = originalMaterials[mesh];
            }

            try
            {
                return operation(Scene);
            }
            finally
            {
                ApplyRenderMode(displayMode);
            }
        }

        public void ApplyVisibility(SceneVisibilityState state)
        {
            foreach (ModelViewerSceneItem root in SceneItems)
                ApplyVisibility(root, state, true);
        }

        public void SetSelected(ModelViewerSceneItem? item)
        {
            foreach (MeshNode mesh in selectedMeshes)
                mesh.PostEffects = string.Empty;
            selectedMeshes.Clear();

            foreach (ModelViewerSceneItem sceneItem in itemByNode.Values)
                sceneItem.IsSelected = ReferenceEquals(sceneItem, item);

            if (item == null)
                return;

            ModelViewerSceneItem? ancestor = item.Parent;
            while (ancestor != null)
            {
                ancestor.IsExpanded = true;
                ancestor = ancestor.Parent;
            }

            foreach (MeshNode mesh in item.SelfAndDescendants().Select(sceneItem => sceneItem.Node).OfType<MeshNode>())
            {
                mesh.PostEffects = "selection[color:#FF3899F6]";
                selectedMeshes.Add(mesh);
            }
        }

        public static IReadOnlyCollection<Guid> GetIsolationScope(ModelViewerSceneItem item)
        {
            HashSet<Guid> ids = new(item.SelfAndDescendants().Select(sceneItem => sceneItem.Id));
            ModelViewerSceneItem? ancestor = item.Parent;
            while (ancestor != null)
            {
                ids.Add(ancestor.Id);
                ancestor = ancestor.Parent;
            }
            return ids;
        }

        public void ApplyFilter(string? searchText)
        {
            string text = searchText?.Trim() ?? string.Empty;
            foreach (ModelViewerSceneItem root in SceneItems)
                ApplyFilter(root, text);
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            selectedMeshes.Clear();
            Root.Dispose();
        }

        private ModelViewerSceneItem BuildTree(SceneNode node, ModelViewerSceneItem? parent, string fallbackName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MeshGeometry3D? geometry = (node as MeshNode)?.Geometry as MeshGeometry3D;
            string name = string.IsNullOrWhiteSpace(node.Name)
                ? parent == null ? fallbackName : node is MeshNode ? $"Mesh {itemByNode.Count + 1}" : $"Group {itemByNode.Count + 1}"
                : node.Name;

            ModelViewerSceneItem item = new()
            {
                Id = node.GUID,
                Name = name,
                Node = node,
                IsMesh = node is MeshNode,
                Parent = parent,
                VertexCount = geometry?.Positions?.Count ?? 0,
                TriangleCount = (geometry?.Indices?.Count ?? 0) / 3,
                MaterialName = (node as MeshNode)?.Material?.Name ?? string.Empty,
                IsExpanded = parent == null,
            };
            itemByNode[node] = item;
            foreach (SceneNode child in node.Items)
                item.Children.Add(BuildTree(child, item, fallbackName, cancellationToken));
            return item;
        }

        private static bool ApplyFilter(ModelViewerSceneItem item, string searchText)
        {
            bool childMatches = false;
            foreach (ModelViewerSceneItem child in item.Children)
                childMatches |= ApplyFilter(child, searchText);

            bool selfMatches = searchText.Length == 0 || item.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase);
            item.IsVisibleInFilter = selfMatches || childMatches;
            if (searchText.Length > 0 && childMatches)
                item.IsExpanded = true;
            return item.IsVisibleInFilter;
        }

        private static void ApplyVisibility(ModelViewerSceneItem item, SceneVisibilityState state, bool parentVisible)
        {
            bool visible = parentVisible && state.IsVisible(item.Id);
            item.Node.Visible = visible;
            item.Node.IsHitTestVisible = visible;
            item.IsEffectivelyVisible = visible;
            foreach (ModelViewerSceneItem child in item.Children)
                ApplyVisibility(child, state, visible);
        }

        private static string? GetTexturePath(MaterialCore? material)
        {
            return material switch
            {
                PhongMaterialCore phong => phong.DiffuseMapFilePath,
                DiffuseMaterialCore diffuse => diffuse.DiffuseMapFilePath,
                PBRMaterialCore pbr => pbr.AlbedoMapFilePath,
                _ => null,
            };
        }

        private static string ResolveTexturePath(string modelPath, string texturePath)
        {
            if (texturePath.StartsWith('*') || Path.IsPathRooted(texturePath))
                return texturePath;
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(modelPath) ?? string.Empty, texturePath));
        }

        private static bool IsFinite(BoundingBox bounds)
        {
            return float.IsFinite(bounds.Minimum.X) && float.IsFinite(bounds.Minimum.Y) && float.IsFinite(bounds.Minimum.Z)
                && float.IsFinite(bounds.Maximum.X) && float.IsFinite(bounds.Maximum.Y) && float.IsFinite(bounds.Maximum.Z);
        }

        private static bool SuggestVerticalFlip(IReadOnlyList<MeshNode> meshes, BoundingBox bounds, CancellationToken cancellationToken)
        {
            float width = bounds.Maximum.X - bounds.Minimum.X;
            float depth = bounds.Maximum.Y - bounds.Minimum.Y;
            float height = bounds.Maximum.Z - bounds.Minimum.Z;
            if (height <= Math.Max(width, depth) * 1.2f || height <= float.Epsilon)
                return false;

            float lowerThreshold = bounds.Minimum.Z + height * 0.1f;
            float upperThreshold = bounds.Maximum.Z - height * 0.1f;
            long lowerCount = 0;
            long upperCount = 0;
            foreach (MeshNode mesh in meshes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (mesh.Geometry is not MeshGeometry3D geometry || geometry.Positions == null)
                    continue;

                System.Numerics.Matrix4x4 transform = mesh.TotalModelMatrix;
                int positionIndex = 0;
                foreach (System.Numerics.Vector3 position in geometry.Positions)
                {
                    if ((positionIndex++ & 8191) == 0)
                        cancellationToken.ThrowIfCancellationRequested();
                    float z = System.Numerics.Vector3.Transform(position, transform).Z;
                    if (z <= lowerThreshold)
                        lowerCount++;
                    if (z >= upperThreshold)
                        upperCount++;
                }
            }

            return ModelOrientationHeuristics.ShouldFlipVertical(width, depth, height, lowerCount, upperCount);
        }
    }

    internal static class ModelOrientationHeuristics
    {
        public static bool ShouldFlipVertical(float width, float depth, float height, long lowerBandVertexCount, long upperBandVertexCount)
        {
            const long minimumEvidence = 100;
            return height > Math.Max(width, depth) * 1.2f
                && lowerBandVertexCount >= minimumEvidence
                && upperBandVertexCount >= minimumEvidence
                && upperBandVertexCount < lowerBandVertexCount * 0.72;
        }
    }

    internal sealed class ModelViewerLoadException : Exception
    {
        public ModelViewerLoadException(string message) : base(message)
        {
        }
    }

    internal static class ModelViewer3DLoader
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".obj", ".stl" };
        private static readonly SemaphoreSlim ImportGate = new(1, 1);

        public static async Task<ModelViewer3DModel> LoadAsync(string filePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Model path cannot be empty.", nameof(filePath));
            if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
                throw new NotSupportedException($"Unsupported 3D model format: {Path.GetExtension(filePath)}");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("The model file does not exist.", filePath);

            string fullPath = Path.GetFullPath(filePath);
            await ImportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(() => LoadCore(fullPath, cancellationToken), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ImportGate.Release();
            }
        }

        public static async Task<ErrorCode> ExportAsync(string sourceFilePath, string destinationFilePath, string formatId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
                throw new ArgumentException("Model path cannot be empty.", nameof(sourceFilePath));
            if (string.IsNullOrWhiteSpace(destinationFilePath))
                throw new ArgumentException("Export path cannot be empty.", nameof(destinationFilePath));
            if (string.IsNullOrWhiteSpace(formatId))
                throw new ArgumentException("Export format cannot be empty.", nameof(formatId));
            if (!SupportedExtensions.Contains(Path.GetExtension(sourceFilePath)))
                throw new NotSupportedException($"Unsupported 3D model format: {Path.GetExtension(sourceFilePath)}");
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("The model file does not exist.", sourceFilePath);

            string fullSourcePath = Path.GetFullPath(sourceFilePath);
            string fullDestinationPath = Path.GetFullPath(destinationFilePath);
            await ImportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(() => ExportCore(fullSourcePath, fullDestinationPath, formatId, cancellationToken), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ImportGate.Release();
            }
        }

        private static ModelViewer3DModel LoadCore(string filePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stopwatch stopwatch = Stopwatch.StartNew();
            using Importer importer = new();
            ErrorCode errorCode = importer.Load(filePath, out HelixToolkitScene? scene);
            stopwatch.Stop();

            if (!errorCode.HasFlag(ErrorCode.Succeed) || scene?.Root == null)
            {
                scene?.Root?.Dispose();
                throw new ModelViewerLoadException($"Assimp failed to import the model ({errorCode}).");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                scene.Root.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }

            ModelViewer3DModel model;
            try
            {
                model = new ModelViewer3DModel(filePath, scene, stopwatch.Elapsed, cancellationToken);
            }
            catch
            {
                scene.Root.Dispose();
                throw;
            }

            if (!cancellationToken.IsCancellationRequested)
                return model;

            model.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
            return model;
        }

        private static ErrorCode ExportCore(string sourceFilePath, string destinationFilePath, string formatId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using Importer importer = new();
            ErrorCode importResult = importer.Load(sourceFilePath, out HelixToolkitScene? scene);
            if (!importResult.HasFlag(ErrorCode.Succeed) || scene?.Root == null)
            {
                scene?.Root?.Dispose();
                throw new ModelViewerLoadException($"Assimp failed to import the model for export ({importResult}).");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using Exporter exporter = new();
                return exporter.ExportToFile(destinationFilePath, scene, formatId);
            }
            finally
            {
                scene.Root.Dispose();
            }
        }
    }
}
