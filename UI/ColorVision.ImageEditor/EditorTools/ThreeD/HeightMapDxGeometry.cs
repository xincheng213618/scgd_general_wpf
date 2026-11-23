using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using System;
using System.Numerics;
using System.Threading;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    /// <summary>Immutable CPU cache shared by upload, picking, and on-demand export.</summary>
    internal sealed class HeightMapDxGeometry
    {
        private const byte VisibleAlphaThreshold = 127;

        private readonly Vector3Collection positions;
        private readonly Vector3Collection normals;
        private readonly Vector2Collection textureCoordinates;
        private readonly IntCollection indices;
        private readonly bool[]? visibleCells;

        public HeightMapSample Sample { get; }
        public ReadOnlySpan<Vector3> Positions => positions.GetInternalArray().AsSpan(0, positions.Count);
        public ReadOnlySpan<Vector3> Normals => normals.GetInternalArray().AsSpan(0, normals.Count);
        public ReadOnlySpan<Vector2> TextureCoordinates => textureCoordinates.GetInternalArray().AsSpan(0, textureCoordinates.Count);
        public ReadOnlySpan<int> Indices => indices.GetInternalArray().AsSpan(0, indices.Count);

        internal HeightMapDxGeometry(HeightMapSample sample, Vector3Collection positions, Vector3Collection normals,
            Vector2Collection textureCoordinates, IntCollection indices, bool[]? visibleCells)
        {
            Sample = sample;
            this.positions = positions;
            this.normals = normals;
            this.textureCoordinates = textureCoordinates;
            this.indices = indices;
            this.visibleCells = visibleCells;
        }

        // Keep the mesh non-transient so upload retains the buffers used for picking and export.
        internal MeshGeometry3D CreateMesh() => new()
        {
            Positions = positions, Normals = normals, TextureCoordinates = textureCoordinates, Indices = indices,
        };

        public static HeightMapDxGeometry Build(HeightMapSample sample, double worldWidth, double worldHeight,
            CancellationToken cancellationToken, HeightMapSample? transparencySource = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int width = sample.Width;
            int height = sample.Height;
            int count = checked(width * height);
            var meshPositions = new Vector3Collection(count);
            var meshNormals = new Vector3Collection(count);
            var meshTextureCoordinates = new Vector2Collection(count);
            Vector3[] positions = ResizeBuffer(meshPositions, count);
            Vector3[] normals = ResizeBuffer(meshNormals, count);
            Vector2[] uv = ResizeBuffer(meshTextureCoordinates, count);
            int cellCount = checked((width - 1) * (height - 1));
            float dx = (float)(worldWidth / (width - 1));
            float dy = (float)(worldHeight / (height - 1));
            HeightMapSample maskSource = transparencySource ?? sample;
            byte[]? mask = maskSource.Alpha;
            // A missing visibility array means every mesh cell is visible.
            bool[]? visibleCells = mask != null || width <= 0 || height <= 0 ? new bool[cellCount] : null;
            int visibleCount = visibleCells == null ? cellCount : 0;
            bool maskMatchesMesh = maskSource.Width == width && maskSource.Height == height;
            for (int y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int cellsInRow = visibleCells != null && y + 1 < height ? width - 1 : 0;
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    byte gray = sample.Gray[index];
                    positions[index] = new Vector3(x * dx, (height - 1 - y) * dy, gray / 255f);
                    uv[index] = new Vector2((gray + 0.5f) / 256f, 0.5f);
                    int left = x > 0 && IsVisible(sample, index - 1) ? index - 1 : index;
                    int right = x + 1 < width && IsVisible(sample, index + 1) ? index + 1 : index;
                    int top = y > 0 && IsVisible(sample, index - width) ? index - width : index;
                    int bottom = y + 1 < height && IsVisible(sample, index + width) ? index + width : index;
                    float gradientX = (sample.Gray[right] - sample.Gray[left]) / (255f * Math.Max(1, right - left) * dx);
                    float gradientY = (sample.Gray[bottom] - sample.Gray[top]) / (255f * Math.Max(1, (bottom - top) / width) * dy);
                    normals[index] = Vector3.Normalize(new Vector3(-gradientX, gradientY, 1));
                    if (x < cellsInRow)
                    {
                        bool visible = maskMatchesMesh
                            ? mask![index] > VisibleAlphaThreshold && mask[index + 1] > VisibleAlphaThreshold
                                && mask[index + width] > VisibleAlphaThreshold && mask[index + width + 1] > VisibleAlphaThreshold
                            : IsCoarseCellVisible(maskSource, x, y, width, height);
                        visibleCells![y * (width - 1) + x] = visible;
                        if (visible) visibleCount++;
                    }
                }
            }

            int indexCount = checked(visibleCount * 6);
            var meshIndices = new IntCollection(indexCount);
            int[] indices = ResizeBuffer(meshIndices, indexCount);
            int cursor = 0;
            for (int y = 0; y < height - 1; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < width - 1; x++)
                {
                    if (visibleCells != null && !visibleCells[y * (width - 1) + x]) continue;
                    int topLeft = y * width + x;
                    int bottomLeft = topLeft + width;
                    indices[cursor++] = topLeft;
                    indices[cursor++] = bottomLeft;
                    indices[cursor++] = topLeft + 1;
                    indices[cursor++] = topLeft + 1;
                    indices[cursor++] = bottomLeft;
                    indices[cursor++] = bottomLeft + 1;
                }
            }
            return new HeightMapDxGeometry(sample, meshPositions, meshNormals, meshTextureCoordinates, meshIndices, visibleCells);
        }

        private static T[] ResizeBuffer<T>(FastList<T> collection, int count)
        {
            collection.Resize(count, fastClear: true);
            return collection.GetInternalArray();
        }

        public static HeightMapSample Reduce(HeightMapSample source, int maxWidth, int maxHeight, CancellationToken token)
        {
            (int width, int height) = HeightMapPixelSampler.CalculateFitSize(source.Width, source.Height, maxWidth, maxHeight);
            if (width == source.Width && height == source.Height) return source;
            token.ThrowIfCancellationRequested();
            var gray = new byte[width * height];
            byte[]? sourceAlpha = source.Alpha;
            byte[]? alpha = sourceAlpha == null ? null : new byte[gray.Length];
            // The renderer's interaction widths need at most 4 KiB of column indices.
            Span<int> sourceColumns = width <= 1024 ? stackalloc int[width] : new int[width];
            for (int x = 0; x < width; x++)
                sourceColumns[x] = (int)Math.Round(x * (source.Width - 1d) / (width - 1));
            for (int y = 0; y < height; y++)
            {
                token.ThrowIfCancellationRequested();
                int sy = (int)Math.Round(y * (source.Height - 1d) / (height - 1));
                int sourceRow = sy * source.Width;
                int targetRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    int sourceIndex = sourceRow + sourceColumns[x];
                    gray[targetRow + x] = source.Gray[sourceIndex];
                    if (alpha != null) alpha[targetRow + x] = sourceAlpha![sourceIndex];
                }
            }
            return new HeightMapSample(gray, alpha, width, height);
        }

        private static bool IsVisible(HeightMapSample sample, int index) => sample.Alpha == null || sample.Alpha[index] > VisibleAlphaThreshold;

        private static bool IsCoarseCellVisible(HeightMapSample source, int x, int y, int width, int height)
        {
            // A coarse quad can cover a transparent hole between its corners.
            int left = (int)Math.Floor(x * (source.Width - 1d) / (width - 1));
            int right = (int)Math.Ceiling((x + 1) * (source.Width - 1d) / (width - 1));
            int top = (int)Math.Floor(y * (source.Height - 1d) / (height - 1));
            int bottom = (int)Math.Ceiling((y + 1) * (source.Height - 1d) / (height - 1));
            byte[] mask = source.Alpha!;
            for (int sy = top; sy <= bottom; sy++)
            {
                int rowOffset = sy * source.Width;
                for (int sx = left; sx <= right; sx++)
                    if (mask[rowOffset + sx] <= VisibleAlphaThreshold) return false;
            }
            return true;
        }

        /// <summary>Visits only the grid cells crossed by the ray, instead of all triangles.</summary>
        public bool TryIntersect(Vector3 origin, Vector3 direction, double worldWidth, double worldHeight,
            double heightScale, out Vector3 point)
        {
            point = default;
            // Work in sample-grid coordinates. Z stays in displayed world units.
            double ox = origin.X * (Sample.Width - 1) / worldWidth;
            double oy = (worldHeight - origin.Y) * (Sample.Height - 1) / worldHeight;
            double vx = direction.X * (Sample.Width - 1) / worldWidth;
            double vy = -direction.Y * (Sample.Height - 1) / worldHeight;
            double enter = 0;
            double leave = double.PositiveInfinity;
            if (!Clip(ox, vx, Sample.Width - 1, ref enter, ref leave)
                || !Clip(oy, vy, Sample.Height - 1, ref enter, ref leave)
                || !Clip(origin.Z, direction.Z, heightScale, ref enter, ref leave)) return false;
            double startX = Math.Clamp(ox + vx * enter, 0, Sample.Width - 1);
            double startY = Math.Clamp(oy + vy * enter, 0, Sample.Height - 1);
            int x = Math.Clamp((int)Math.Floor(startX + Math.Sign(vx) * 1e-7), 0, Sample.Width - 2);
            int y = Math.Clamp((int)Math.Floor(startY + Math.Sign(vy) * 1e-7), 0, Sample.Height - 2);
            int stepX = Math.Sign(vx);
            int stepY = Math.Sign(vy);
            double nextX = stepX == 0 ? double.PositiveInfinity : ((stepX > 0 ? x + 1 : x) - ox) / vx;
            double nextY = stepY == 0 ? double.PositiveInfinity : ((stepY > 0 ? y + 1 : y) - oy) / vy;
            double incrementX = stepX == 0 ? double.PositiveInfinity : Math.Abs(1 / vx);
            double incrementY = stepY == 0 ? double.PositiveInfinity : Math.Abs(1 / vy);
            ReadOnlySpan<Vector3> points = Positions;
            for (int visited = 0; visited <= Sample.Width + Sample.Height; visited++)
            {
                double cellExit = Math.Min(leave, Math.Min(nextX, nextY));
                if (visibleCells == null || visibleCells[y * (Sample.Width - 1) + x])
                {
                    int a = y * Sample.Width + x;
                    int c = a + Sample.Width;
                    Vector3 p0 = Scaled(points[a], heightScale);
                    Vector3 p1 = Scaled(points[a + 1], heightScale);
                    Vector3 p2 = Scaled(points[c], heightScale);
                    Vector3 p3 = Scaled(points[c + 1], heightScale);
                    double nearest = double.PositiveInfinity;
                    if (Triangle(origin, direction, p0, p2, p1, out double t0) && t0 >= enter - 1e-4 && t0 <= cellExit + 1e-4) nearest = t0;
                    if (Triangle(origin, direction, p1, p2, p3, out double t1) && t1 >= enter - 1e-4 && t1 <= cellExit + 1e-4) nearest = Math.Min(nearest, t1);
                    if (double.IsFinite(nearest)) { point = origin + direction * (float)nearest; return true; }
                }
                if (cellExit >= leave || double.IsPositiveInfinity(cellExit)) break;
                bool advanceX = nextX <= nextY;
                bool advanceY = nextY <= nextX;
                enter = cellExit;
                if (advanceX) { x += stepX; nextX += incrementX; }
                if (advanceY) { y += stepY; nextY += incrementY; }
                if (x < 0 || x >= Sample.Width - 1 || y < 0 || y >= Sample.Height - 1) break;
            }
            return false;
        }

        private static Vector3 Scaled(Vector3 value, double heightScale) => new(value.X, value.Y, (float)(value.Z * heightScale));

        private static bool Clip(double origin, double direction, double maximum, ref double enter, ref double leave)
        {
            if (Math.Abs(direction) < 1e-12) return origin >= 0 && origin <= maximum;
            double a = -origin / direction;
            double b = (maximum - origin) / direction;
            enter = Math.Max(enter, Math.Min(a, b));
            leave = Math.Min(leave, Math.Max(a, b));
            return leave >= enter;
        }

        private static bool Triangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out double distance)
        {
            distance = 0;
            // CPU picking uses double intermediates so tall, narrow triangles do not develop
            // missed cells from cancellation in float cross products. GPU positions stay float.
            double e1x = (double)b.X - a.X, e1y = (double)b.Y - a.Y, e1z = (double)b.Z - a.Z;
            double e2x = (double)c.X - a.X, e2y = (double)c.Y - a.Y, e2z = (double)c.Z - a.Z;
            double hx = direction.Y * e2z - direction.Z * e2y;
            double hy = direction.Z * e2x - direction.X * e2z;
            double hz = direction.X * e2y - direction.Y * e2x;
            double determinant = e1x * hx + e1y * hy + e1z * hz;
            if (Math.Abs(determinant) < 1e-12) return false;
            double inverse = 1 / determinant;
            double sx = (double)origin.X - a.X, sy = (double)origin.Y - a.Y, sz = (double)origin.Z - a.Z;
            double u = (sx * hx + sy * hy + sz * hz) * inverse;
            if (u < -1e-6 || u > 1.000001) return false;
            double qx = sy * e1z - sz * e1y;
            double qy = sz * e1x - sx * e1z;
            double qz = sx * e1y - sy * e1x;
            double v = (direction.X * qx + direction.Y * qy + direction.Z * qz) * inverse;
            if (v < -1e-6 || u + v > 1.000001) return false;
            distance = (e2x * qx + e2y * qy + e2z * qz) * inverse;
            return distance >= 0;
        }
    }
}
