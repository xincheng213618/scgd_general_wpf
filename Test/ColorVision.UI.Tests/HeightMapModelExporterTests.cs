using ColorVision.ImageEditor.EditorTools.ThreeD;
using SharpAssimp;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class HeightMapModelExporterTests
{
    [Fact]
    public async Task Obj_PreservesScaledVerticesNormalsUvsAndLutUnderCommaDecimalCulture()
    {
        using var output = new ExportDirectory();
        string path = Path.Combine(output.Path, "模型 测试.obj");
        byte[] lut = CreateLut();
        byte[] expectedLut = (byte[])lut.Clone();
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Task export = HeightMapModelExporter.ExportAsync(Triangle(), lut, 4, path, true, CancellationToken.None);
            Array.Fill(lut, (byte)0); // The exporter must own the small palette snapshot.
            await export;
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }

        string[] lines = await File.ReadAllLinesAsync(path);
        string[] positions = lines.Where(line => line.StartsWith("v ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(new[] { "v 0.25 0.5 1", "v 1.25 0.5 3", "v 0.25 2.5 1" }, positions);
        Assert.Equal(new[] { "vt 0.125 0.5", "vt 0.375 0.5", "vt 0.625 0.5" },
            lines.Where(line => line.StartsWith("vt ", StringComparison.Ordinal)));
        Assert.Equal("f 1/1/1 2/2/2 3/3/3", Assert.Single(lines, line => line.StartsWith("f ", StringComparison.Ordinal)));
        Vector3 expectedNormal = Vector3.Normalize(new Vector3(-2, 0, 1));
        foreach (string line in lines.Where(line => line.StartsWith("vn ", StringComparison.Ordinal)))
            AssertVectorClose(expectedNormal, ParseVector(line));

        string materialName = Assert.Single(lines, line => line.StartsWith("mtllib ", StringComparison.Ordinal))[7..];
        Assert.DoesNotContain(' ', materialName);
        string[] material = await File.ReadAllLinesAsync(Path.Combine(output.Path, materialName));
        string textureName = Assert.Single(material, line => line.StartsWith("map_Kd ", StringComparison.Ordinal))[7..];
        using var textureStream = File.OpenRead(Path.Combine(output.Path, textureName));
        BitmapSource texture = BitmapDecoder.Create(textureStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        Assert.Equal(256, texture.PixelWidth);
        Assert.Equal(1, texture.PixelHeight);
        var bgr = new FormatConvertedBitmap(texture, PixelFormats.Bgr24, null, 0);
        byte[] actualLut = new byte[256 * 3];
        bgr.CopyPixels(actualLut, actualLut.Length, 0);
        Assert.Equal(expectedLut, actualLut);
        Assert.Equal(3, Directory.GetFiles(output.Path).Length);
    }

    [Fact]
    public async Task BinaryStl_HasStandardLengthScaledCoordinatesAndGeometricFaceNormal()
    {
        using var output = new ExportDirectory();
        string path = Path.Combine(output.Path, "triangle.stl");

        await HeightMapModelExporter.ExportAsync(Triangle(), CreateLut(), 4, path, true, CancellationToken.None);

        using var reader = new BinaryReader(File.OpenRead(path));
        Assert.Equal(84 + 50, reader.BaseStream.Length);
        Assert.Equal(80, reader.ReadBytes(80).Length);
        Assert.Equal(1u, reader.ReadUInt32());
        AssertVectorClose(Vector3.Normalize(new Vector3(-2, 0, 1)), ReadVector(reader));
        Assert.Equal(new Vector3(.25f, .5f, 1), ReadVector(reader));
        Assert.Equal(new Vector3(1.25f, .5f, 3), ReadVector(reader));
        Assert.Equal(new Vector3(.25f, 2.5f, 1), ReadVector(reader));
        Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        Assert.Single(Directory.GetFiles(output.Path));
    }

    [Theory]
    [InlineData(".obj")]
    [InlineData(".stl")]
    public async Task Export_PreservesTransparentHoleIndices(string extension)
    {
        using var output = new ExportDirectory();
        string path = Path.Combine(output.Path, "hole" + extension);
        var sample = new HeightMapSample([0, 64, 128, 0, 64, 128], [255, 255, 0, 255, 255, 0], 3, 2);
        HeightMapDxGeometry geometry = HeightMapDxGeometry.Build(sample, 2, 1, CancellationToken.None);

        await HeightMapModelExporter.ExportAsync(geometry, null, 10, path, true, CancellationToken.None);

        if (extension == ".obj")
        {
            string[] faces = (await File.ReadAllLinesAsync(path)).Where(line => line.StartsWith("f ", StringComparison.Ordinal)).ToArray();
            Assert.Equal(new[] { "f 1/1/1 4/4/4 2/2/2", "f 2/2/2 4/4/4 5/5/5" }, faces);
            Assert.Empty(Directory.GetFiles(output.Path, "*.png"));
            Assert.DoesNotContain("map_Kd", await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(output.Path, "*.mtl"))));
        }
        else
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            Assert.Equal(84 + 2 * 50, reader.BaseStream.Length);
            reader.BaseStream.Position = 80;
            Assert.Equal(2u, reader.ReadUInt32());
        }
    }

    [Theory]
    [InlineData(".obj")]
    [InlineData(".stl")]
    public async Task Export_RoundTripsThroughExistingAssimpReader(string extension)
    {
        using var output = new ExportDirectory();
        string path = Path.Combine(output.Path, "model triangle" + extension);
        await HeightMapModelExporter.ExportAsync(Triangle(), CreateLut(), 4, path, true, CancellationToken.None);

        using var importer = new AssimpContext();
        Scene scene = importer.ImportFile(path);

        Mesh mesh = Assert.Single(scene.Meshes);
        Assert.Single(mesh.Faces);
        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Contains(new Vector3(.25f, .5f, 1), mesh.Vertices);
        Assert.Contains(new Vector3(1.25f, .5f, 3), mesh.Vertices);
        Assert.Contains(new Vector3(.25f, 2.5f, 1), mesh.Vertices);
        if (extension == ".obj")
        {
            Material material = scene.Materials[mesh.MaterialIndex];
            Assert.True(material.HasTextureDiffuse);
            Assert.True(File.Exists(Path.Combine(output.Path, material.TextureDiffuse.FilePath)));
            Assert.True(mesh.HasNormals);
            Assert.True(mesh.HasTextureCoords(0));
        }
    }

    [Fact]
    public async Task Export_AlreadyCanceledDoesNotReplaceExistingFile()
    {
        using var output = new ExportDirectory();
        string path = Path.Combine(output.Path, "existing.obj");
        File.WriteAllText(path, "existing file");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            HeightMapModelExporter.ExportAsync(Triangle(), null, 4, path, true, cancellation.Token));

        Assert.Equal("existing file", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(output.Path));
    }

    [Theory]
    [InlineData(".obj")]
    [InlineData(".stl")]
    public async Task Export_InvalidIndexLeavesExistingFileIntactAndRemovesTemporaryFile(string extension)
    {
        using var output = new ExportDirectory();
        string path = Path.Combine(output.Path, "existing" + extension);
        await File.WriteAllTextAsync(path, "existing file");
        HeightMapDxGeometry invalid = Triangle();
        invalid.Indices[1] = 99;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            HeightMapModelExporter.ExportAsync(invalid, CreateLut(), 4, path, true, CancellationToken.None));

        Assert.Equal("existing file", await File.ReadAllTextAsync(path));
        Assert.Single(Directory.GetFiles(output.Path));
    }

    [Theory]
    [InlineData(".obj")]
    [InlineData(".stl")]
    public async Task Export_ZeroHeightWritesFiniteFlatGeometry(string extension)
    {
        using var output = new ExportDirectory();
        string path = Path.Combine(output.Path, "flat" + extension);
        await HeightMapModelExporter.ExportAsync(Triangle(), null, 0, path, true, CancellationToken.None);
        using var importer = new AssimpContext();
        Scene scene = importer.ImportFile(path);
        Mesh mesh = Assert.Single(scene.Meshes);
        Assert.All(mesh.Vertices, vertex => Assert.Equal(0, vertex.Z));
        Assert.All(mesh.Normals, normal => AssertVectorClose(Vector3.UnitZ, normal));
    }

    [Fact]
    public async Task Obj_DifferentNamesDoNotOverwriteEachOthersCompanionFiles()
    {
        using var output = new ExportDirectory();
        string first = Path.Combine(output.Path, "height map.obj");
        string second = Path.Combine(output.Path, "height_map.obj");
        await HeightMapModelExporter.ExportAsync(Triangle(), CreateLut(), 4, first, true, CancellationToken.None);
        await HeightMapModelExporter.ExportAsync(Triangle(), new byte[256 * 3], 4, second, true, CancellationToken.None);
        string firstMaterial = Assert.Single(await File.ReadAllLinesAsync(first), line => line.StartsWith("mtllib ", StringComparison.Ordinal));
        string secondMaterial = Assert.Single(await File.ReadAllLinesAsync(second), line => line.StartsWith("mtllib ", StringComparison.Ordinal));

        Assert.NotEqual(firstMaterial, secondMaterial);
        Assert.Equal(2, Directory.GetFiles(output.Path, "*.mtl").Length);
        Assert.Equal(2, Directory.GetFiles(output.Path, "*.png").Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Obj_TextureVisibilityUsesCapturedExportOption(bool hideTexture)
    {
        using var output = new ExportDirectory();
        string path = Path.Combine(output.Path, "texture.obj");

        await HeightMapModelExporter.ExportAsync(Triangle(), CreateLut(), 4, path, hideTexture, CancellationToken.None);

        string texture = Assert.Single(Directory.GetFiles(output.Path, "*.png"));
        Assert.Equal(hideTexture, (File.GetAttributes(texture) & FileAttributes.Hidden) != 0);
    }

    private static HeightMapDxGeometry Triangle()
    {
        Vector3 normal = Vector3.Normalize(new Vector3(-.5f, 0, 1));
        return new HeightMapDxGeometry
        {
            Sample = new HeightMapSample([64, 191, 64], null, 3, 1),
            Positions = [new(.25f, .5f, .25f), new(1.25f, .5f, .75f), new(.25f, 2.5f, .25f)],
            Normals = [normal, normal, normal],
            TextureCoordinates = [new(.125f, .5f), new(.375f, .5f), new(.625f, .5f)],
            Indices = [0, 1, 2],
            VisibleCells = [],
        };
    }

    private static byte[] CreateLut()
    {
        byte[] lut = new byte[256 * 3];
        for (int i = 0; i < 256; i++)
        {
            lut[i * 3] = (byte)i;
            lut[i * 3 + 1] = (byte)(i % 5 * 50);
            lut[i * 3 + 2] = (byte)(255 - i);
        }
        return lut;
    }

    private static Vector3 ParseVector(string line)
    {
        string[] parts = line.Split(' ');
        return new Vector3(float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture),
            float.Parse(parts[3], CultureInfo.InvariantCulture));
    }

    private static Vector3 ReadVector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    private static void AssertVectorClose(Vector3 expected, Vector3 actual) => Assert.InRange(Vector3.Distance(expected, actual), 0, 1e-6f);

    private sealed class ExportDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ColorVision.HeightMapExport." + Guid.NewGuid().ToString("N"));
        public ExportDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            foreach (string file in Directory.EnumerateFiles(Path)) File.Delete(file);
            Directory.Delete(Path);
        }
    }
}
