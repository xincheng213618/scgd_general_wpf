using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    /// <summary>Streams the immutable detail mesh; neither camera transforms nor reduced interaction geometry are exported.</summary>
    internal static class HeightMapModelExporter
    {
        public static Task ExportAsync(HeightMapDxGeometry geometry, byte[]? bgrLut, double heightScale,
            string filePath, bool hideTexture, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(geometry);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            if (!double.IsFinite(heightScale) || heightScale < 0)
                throw new ArgumentOutOfRangeException(nameof(heightScale));
            if (geometry.Positions.Length != geometry.Normals.Length
                || geometry.Positions.Length != geometry.TextureCoordinates.Length || geometry.Indices.Length % 3 != 0)
                throw new ArgumentException("The mesh must contain matching positions, normals, UVs and complete triangles.", nameof(geometry));
            if (bgrLut != null && bgrLut.Length != 256 * 3)
                throw new ArgumentException("Expected a 256-entry BGR lookup table.", nameof(bgrLut));
            string path = Path.GetFullPath(filePath);
            string extension = Path.GetExtension(path);
            bool isStl = extension.Equals(".stl", StringComparison.OrdinalIgnoreCase);
            if (!isStl && !extension.Equals(".obj", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Height maps can be exported as OBJ or STL.", nameof(filePath));
            cancellationToken.ThrowIfCancellationRequested();
            byte[]? lutSnapshot = bgrLut == null ? null : (byte[])bgrLut.Clone();
            return Task.Run(() => Export(geometry, lutSnapshot, heightScale, path, isStl, hideTexture, cancellationToken), cancellationToken);
        }

        private static void Export(HeightMapDxGeometry geometry, byte[]? lut, double heightScale,
            string filePath, bool isStl, bool hideTexture, CancellationToken token)
        {
            string directory = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(directory);
            string temporarySuffix = $".{Guid.NewGuid():N}.tmp";
            string modelTemporary = filePath + temporarySuffix;
            string materialPath = Path.Combine(directory, AssetStem(filePath) + ".mtl");
            string texturePath = Path.Combine(directory, AssetStem(filePath) + ".png");
            string materialTemporary = materialPath + temporarySuffix;
            string textureTemporary = texturePath + temporarySuffix;
            try
            {
                if (isStl)
                {
                    WriteStl(geometry, heightScale, modelTemporary, token);
                }
                else
                {
                    WriteObj(geometry, heightScale, modelTemporary, Path.GetFileName(materialPath), token);
                    WriteMaterial(materialTemporary, lut == null ? null : Path.GetFileName(texturePath));
                    if (lut != null) WriteTexture(lut, textureTemporary);
                }
                token.ThrowIfCancellationRequested();
                // Complete every file before replacing any destination. Promote the model last;
                // a canceled/failed write never replaces an existing model with a partial file.
                if (!isStl)
                {
                    if (lut != null)
                    {
                        File.Move(textureTemporary, texturePath, overwrite: true);
                        if (hideTexture) TryHideTexture(texturePath);
                    }
                    File.Move(materialTemporary, materialPath, overwrite: true);
                }
                File.Move(modelTemporary, filePath, overwrite: true);
            }
            finally
            {
                DeleteTemporary(modelTemporary);
                DeleteTemporary(materialTemporary);
                DeleteTemporary(textureTemporary);
            }
        }

        private static string AssetStem(string filePath)
        {
            string originalStem = Path.GetFileNameWithoutExtension(filePath);
            char[] stem = originalStem[..Math.Min(originalStem.Length, 100)].ToCharArray();
            // Wavefront references are whitespace-delimited. Keep companion names usable in
            // viewers which do not support quoted filenames, including source names with spaces.
            for (int i = 0; i < stem.Length; i++)
                if (char.IsWhiteSpace(stem[i]) || stem[i] == '#') stem[i] = '_';
            string suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(originalStem)))[..12];
            return new string(stem) + "_" + suffix + "_heightmap";
        }

        private static StreamWriter CreateTextWriter(string path) => new(
            new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 64 * 1024) { NewLine = "\n" };

        private static void WriteObj(HeightMapDxGeometry geometry, double heightScale,
            string path, string materialName, CancellationToken token)
        {
            using StreamWriter writer = CreateTextWriter(path);
            writer.WriteLine("# ColorVision display-luminance height map");
            writer.Write("mtllib "); writer.WriteLine(materialName);
            writer.WriteLine("o HeightMap");
            writer.WriteLine("usemtl HeightMap");
            for (int i = 0; i < geometry.Positions.Length; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                Vector3 p = geometry.Positions[i];
                WriteVector(writer, "v ", p.X, p.Y, p.Z * heightScale);
            }
            for (int i = 0; i < geometry.TextureCoordinates.Length; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                Vector2 uv = geometry.TextureCoordinates[i];
                writer.Write("vt "); WriteNumber(writer, uv.X); writer.Write(' ');
                WriteNumber(writer, 1 - uv.Y); writer.WriteLine();
            }
            for (int i = 0; i < geometry.Normals.Length; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                Vector3 normal = geometry.Normals[i];
                // Equivalent to inverse-transpose (nx, ny, nz / height), with stable flat-height behavior.
                double x = normal.X * heightScale, y = normal.Y * heightScale, z = normal.Z;
                double largest = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
                if (largest == 0) { z = 1; largest = 1; }
                x /= largest; y /= largest; z /= largest;
                double length = Math.Sqrt(x * x + y * y + z * z);
                WriteVector(writer, "vn ", x / length, y / length, z / length);
            }
            for (int i = 0; i < geometry.Indices.Length; i += 3)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                writer.Write("f ");
                for (int corner = 0; corner < 3; corner++)
                {
                    int index = geometry.Indices[i + corner];
                    ValidateIndex(index, geometry.Positions.Length);
                    if (corner != 0) writer.Write(' ');
                    WriteIndex(writer, index + 1); writer.Write('/');
                    WriteIndex(writer, index + 1); writer.Write('/');
                    WriteIndex(writer, index + 1);
                }
                writer.WriteLine();
            }
        }

        private static void WriteMaterial(string path, string? textureName)
        {
            using StreamWriter writer = CreateTextWriter(path);
            writer.WriteLine("newmtl HeightMap\nKa 0 0 0\nKd 1 1 1\nKs 0 0 0\nd 1\nillum 1");
            if (textureName != null) { writer.Write("map_Kd "); writer.WriteLine(textureName); }
        }

        private static void WriteTexture(byte[] lut, string path)
        {
            var source = BitmapSource.Create(256, 1, 96, 96, PixelFormats.Bgr24, null, lut, 256 * 3);
            source.Freeze();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            encoder.Save(stream);
        }

        private static void WriteStl(HeightMapDxGeometry geometry, double heightScale, string path, CancellationToken token)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
            byte[] header = new byte[80];
            Encoding.ASCII.GetBytes("ColorVision display-luminance height map").CopyTo(header, 0);
            writer.Write(header);
            writer.Write((uint)(geometry.Indices.Length / 3));
            for (int i = 0; i < geometry.Indices.Length; i += 3)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                Vector3 a = ScaledPosition(geometry, geometry.Indices[i], heightScale);
                Vector3 b = ScaledPosition(geometry, geometry.Indices[i + 1], heightScale);
                Vector3 c = ScaledPosition(geometry, geometry.Indices[i + 2], heightScale);
                // Compute the normal from the actual single-precision vertices written to STL,
                // but use double intermediates so large, valid coordinates cannot overflow a cross product.
                double abX = (double)b.X - a.X, abY = (double)b.Y - a.Y, abZ = (double)b.Z - a.Z;
                double acX = (double)c.X - a.X, acY = (double)c.Y - a.Y, acZ = (double)c.Z - a.Z;
                double nx = abY * acZ - abZ * acY, ny = abZ * acX - abX * acZ, nz = abX * acY - abY * acX;
                double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                Vector3 normal = length > 0 ? new Vector3((float)(nx / length), (float)(ny / length), (float)(nz / length)) : Vector3.Zero;
                WriteVector(writer, normal);
                WriteVector(writer, a); WriteVector(writer, b); WriteVector(writer, c);
                writer.Write((ushort)0);
            }
        }

        private static Vector3 ScaledPosition(HeightMapDxGeometry geometry, int index, double heightScale)
        {
            ValidateIndex(index, geometry.Positions.Length);
            Vector3 p = geometry.Positions[index];
            var result = new Vector3(p.X, p.Y, (float)(p.Z * heightScale));
            if (!float.IsFinite(result.X) || !float.IsFinite(result.Y) || !float.IsFinite(result.Z))
                throw new InvalidOperationException("STL coordinates must be finite single-precision values.");
            return result;
        }

        private static void ValidateIndex(int index, int count)
        {
            if ((uint)index >= (uint)count) throw new ArgumentException("A triangle references a vertex outside the mesh.");
        }

        private static void WriteVector(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z);
        }

        private static void WriteVector(TextWriter writer, string prefix, double x, double y, double z)
        {
            writer.Write(prefix); WriteNumber(writer, x); writer.Write(' ');
            WriteNumber(writer, y); writer.Write(' '); WriteNumber(writer, z); writer.WriteLine();
        }

        private static void WriteNumber(TextWriter writer, double value)
        {
            if (!double.IsFinite(value)) throw new InvalidOperationException("OBJ coordinates and normals must be finite.");
            Span<char> buffer = stackalloc char[32];
            value.TryFormat(buffer, out int written, "R", CultureInfo.InvariantCulture);
            writer.Write(buffer[..written]);
        }

        private static void WriteIndex(TextWriter writer, int value)
        {
            Span<char> buffer = stackalloc char[11];
            value.TryFormat(buffer, out int written, provider: CultureInfo.InvariantCulture);
            writer.Write(buffer[..written]);
        }

        private static void TryHideTexture(string path)
        {
            try { File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void DeleteTemporary(string path)
        {
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
