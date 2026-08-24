using ColorVision.ImageEditor.EditorTools.ThreeD;
using HelixToolkit.SharpDX.Assimp;
using System.IO;
using System.Threading;

namespace ColorVision.UI.Tests;

public class ModelViewer3DModelTests
{
    [Fact]
    public async Task ExportScope_UsesOriginalMaterialsAndRestoresTheDisplayedMode()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ColorVision.ModelViewer.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string modelPath = Path.Combine(directory, "triangle.obj");
        string materialPath = Path.Combine(directory, "triangle.mtl");

        try
        {
            await File.WriteAllTextAsync(materialPath, "newmtl Red\nKd 1.0 0.0 0.0\n");
            await File.WriteAllTextAsync(modelPath, "mtllib triangle.mtl\no Triangle\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl Red\nf 1 2 3\n");

            using ModelViewer3DModel model = await ModelViewer3DLoader.LoadAsync(modelPath, CancellationToken.None);
            var mesh = Assert.Single(model.Meshes);
            var originalMaterial = mesh.Material;
            Assert.NotNull(originalMaterial);

            model.ApplyRenderMode(ModelViewerRenderMode.Solid);
            Assert.NotSame(originalMaterial, mesh.Material);

            bool originalWasVisibleToExporter = model.WithOriginalMaterials(
                ModelViewerRenderMode.Solid,
                _ => ReferenceEquals(originalMaterial, mesh.Material));

            Assert.True(originalWasVisibleToExporter);
            Assert.NotSame(originalMaterial, mesh.Material);

            model.ApplyRenderMode(ModelViewerRenderMode.Textured);
            Assert.Same(originalMaterial, mesh.Material);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_ReimportsTheSourceAndWritesAStandaloneFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ColorVision.ModelViewer.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string modelPath = Path.Combine(directory, "triangle.obj");
        string exportPath = Path.Combine(directory, "triangle.stl");

        try
        {
            await File.WriteAllTextAsync(modelPath, "o Triangle\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");

            ErrorCode result = await ModelViewer3DLoader.ExportAsync(modelPath, exportPath, "stl", CancellationToken.None);

            Assert.True(result.HasFlag(ErrorCode.Succeed));
            Assert.True(File.Exists(exportPath));
            Assert.True(new FileInfo(exportPath).Length > 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
