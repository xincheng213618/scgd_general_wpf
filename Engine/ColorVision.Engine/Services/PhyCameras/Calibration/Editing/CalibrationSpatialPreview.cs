using ColorVision.Engine.Services.Types;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Point = System.Windows.Point;

namespace ColorVision.Engine.Services.PhyCameras.Calibration.Editing;

public sealed record CalibrationSpatialSeries(string Name, int ColorIndex, IReadOnlyList<IReadOnlyList<Point>> Lines);

/// <summary>Sparse continuous source-sampling geometry, without frame allocation or pixel interpolation.</summary>
public sealed class CalibrationSpatialPreview
{
    public int Width { get; private init; }
    public int Height { get; private init; }
    public IReadOnlyList<CalibrationSpatialSeries> Series { get; private init; } = Array.Empty<CalibrationSpatialSeries>();
    public IReadOnlyList<IReadOnlyList<Point>> ReferenceGrid { get; private init; } = Array.Empty<IReadOnlyList<Point>>();
    public string Note { get; private init; } = "显示输出网格对应的连续源采样坐标；仅为参数示意，不代表相机图像校正结果。未模拟像素取整、边界填充、插值、翻转或裁剪。";

    public static CalibrationSpatialPreview Create(CalibrationJsonDocument document, int width = 1920, int height = 1080)
    {
        JObject json = JObject.Parse(document.BuildJson());
        double N(string key, double fallback = 0) => json[key]?.Value<double>() ?? fallback;
        double[] A(string key) => json[key] is JArray array && array.Count <= 129 ? array.Select(x => x.Value<double>()).ToArray() : throw new InvalidDataException($"数组 {key} 缺失或过长，预览最多支持 129 个系数。");
        if (document.Type is ServiceTypes.ColorDiff or ServiceTypes.Distortion) { width = (int)N("w"); height = (int)N("h"); }
        if (document.Type == ServiceTypes.AngleShift) { width = (int)N("target_col"); height = (int)N("target_row"); }
        if (width < 3 || height < 3 || width > 65535 || height > 65535) throw new InvalidDataException("预览尺寸须为 3~65535 像素。");
        int outputWidth = document.Type == ServiceTypes.AngleShift ? (int)N("target_col") : width;
        int outputHeight = document.Type == ServiceTypes.AngleShift ? (int)N("target_row") : height;
        bool fish = document.Type == ServiceTypes.Distortion && json["useFisheye"]?.Value<bool>() == true;
        if (fish) { outputWidth = (int)N("s_w", width); outputHeight = (int)N("s_h", height); }
        if (outputWidth < 3 || outputHeight < 3 || outputWidth > 65535 || outputHeight > 65535) throw new InvalidDataException("目标预览尺寸须为 3~65535 像素。");
        List<CalibrationSpatialSeries> series = new();
        void Add(string name, int color, Func<Point, Point?> map) => series.Add(new(name, color, Grid(outputWidth, outputHeight, map, width, height)));
        switch (document.Type)
        {
            case ServiceTypes.ColorShift:
                for (int channel = 0; channel < 3; channel++)
                {
                    JToken offset = json["offset"] is JArray offsets ? offsets[channel]! : json["offset"]!;
                    double dx = (int)offset["X"]!.Value<double>(), dy = (int)offset["Y"]!.Value<double>();
                    Add(new[] { "B", "G", "R" }[channel], 2 - channel, p => new Point(p.X - dx, p.Y - dy));
                }
                break;
            case ServiceTypes.ColorDiff:
                double cx = N("CenterCol"), cy = N("CenterRow"), scale = N("CalibDis") / N("MeasDis");
                Add("G（参考）", 1, p => p);
                foreach ((string suffix, string name, int color) in new[] { ("GR", "R", 0), ("GB", "B", 2) })
                {
                    double[] coefficients = A("ColorDiffCoeffs_" + suffix), shift = A("ColRowCoeffs_" + suffix);
                    Add(name, color, p => { double dx = p.X - cx, dy = p.Y - cy, radius = Math.Sqrt(dx * dx + dy * dy), displacement = Polynomial(coefficients, radius) * scale; if (radius == 0) return new Point(cx - shift[0], cy - displacement - shift[1]); double factor = (radius - displacement) / radius; return new Point(dx * factor + cx - shift[0], dy * factor + cy - shift[1]); });
                }
                break;
            case ServiceTypes.AngleShift:
                double ratio = N("interpolate_ratio"), opticalX = N("optical_center_x") * ratio, opticalY = N("optical_center_y") * ratio;
                double resizedWidth = Math.Truncate(width * ratio), resizedHeight = Math.Truncate(height * ratio);
                if (ratio <= 0 || resizedWidth < 1 || resizedHeight < 1 || resizedWidth > 65535 || resizedHeight > 65535) throw new InvalidDataException("插值后的尺寸超过有效范围。");
                double[] rowColumnShift = A("rowColShift");
                foreach ((string key, string name, int color) in new[] { ("coeff_r", "R", 0), ("coeff_g", "G", 1), ("coeff_b", "B", 2) })
                {
                    double[] coefficients = A(key);
                    Add(name, color, p => { double dx = p.X * ratio - opticalX, dy = p.Y * ratio - opticalY, radius = Math.Sqrt(dx * dx + dy * dy); if (radius > resizedWidth / 2 || radius > resizedHeight / 2) return null; double factor = (radius - Polynomial(coefficients, radius)) / (radius + 1e-12); double sx = factor * dx + opticalX - rowColumnShift[1], sy = factor * dy + opticalY - rowColumnShift[0]; if (sx < 0 || sy < 0 || sx >= resizedWidth || sy >= resizedHeight) return null; return new Point(sx / ratio, sy / ratio); });
                }
                break;
            case ServiceTypes.Distortion:
                double[] camera = A("cameraMatrix"), distortion = A("distCoeffs");
                using (Mat cameraMat = new(3, 3, MatType.CV_32FC1))
                using (Mat distortionMat = new(fish ? 4 : 5, 1, MatType.CV_32FC1))
                {
                    for (int row = 0; row < 3; row++) for (int column = 0; column < 3; column++) cameraMat.Set(row, column, CheckedFloat(camera[row * 3 + column]));
                    for (int i = 0; i < (fish ? 4 : 5); i++) distortionMat.Set(i, 0, CheckedFloat(i < distortion.Length ? distortion[i] : 0));
                    float centerX = CheckedFloat(camera[2]), centerY = CheckedFloat(camera[5]);
                    if (fish && (Math.Abs((double)centerX) > 1e9 || Math.Abs((double)centerY) > 1e9)) throw new InvalidDataException("鱼眼主点坐标过大，无法预览中心平移。");
                    int offsetX = fish ? width / 2 - (int)centerX : 0, offsetY = fish ? height / 2 - (int)centerY : 0;
                    using Mat newCamera = new();
                    if (fish)
                    {
                        cameraMat.Set(0, 2, (float)(width / 2)); cameraMat.Set(1, 2, (float)(height / 2));
                        using Mat identity = Mat.Eye(3, 3, MatType.CV_64FC1).ToMat();
                        Cv2.FishEye.EstimateNewCameraMatrixForUndistortRectify(cameraMat, distortionMat, new Size(width, height), identity, newCamera, N("alpha"), new Size(outputWidth, outputHeight), 1.0);
                    }
                    else
                    {
                        using Mat optimal = Cv2.GetOptimalNewCameraMatrix(cameraMat, distortionMat, new Size(width, height), N("alpha"), new Size(width, height), out _);
                        optimal.CopyTo(newCamera);
                    }
                    using Mat newCameraDouble = new(); using Mat inverseDouble = new();
                    newCamera.ConvertTo(newCameraDouble, MatType.CV_64FC1);
                    if (Cv2.Invert(newCameraDouble, inverseDouble) == 0) throw new InvalidDataException("输出相机矩阵不可逆，无法预览。");
                    double[] inv = new double[9]; for (int row = 0; row < 3; row++) for (int column = 0; column < 3; column++) inv[row * 3 + column] = inverseDouble.At<double>(row, column);
                    double[] k = new double[5]; for (int i = 0; i < 5; i++) k[i] = CheckedFloat(i < distortion.Length ? distortion[i] : 0);
                    double fx = CheckedFloat(camera[0]), fy = CheckedFloat(camera[4]), pcx = CheckedFloat(camera[2]), pcy = CheckedFloat(camera[5]);
                    Add("源采样网格", 0, p =>
                    {
                        double z = inv[6] * p.X + inv[7] * p.Y + inv[8], x = (inv[0] * p.X + inv[1] * p.Y + inv[2]) / z, y = (inv[3] * p.X + inv[4] * p.Y + inv[5]) / z, r2 = x * x + y * y;
                        if (fish)
                        {
                            double radius = Math.Sqrt(r2), theta = Math.Atan(radius), theta2 = theta * theta;
                            double thetaDistorted = theta * (1 + k[0] * theta2 + k[1] * theta2 * theta2 + k[2] * theta2 * theta2 * theta2 + k[3] * theta2 * theta2 * theta2 * theta2);
                            double factor = radius > 1e-12 ? thetaDistorted / radius : 1;
                            double sx = fx * x * factor + width / 2 - Math.Max(0, offsetX), sy = fy * y * factor + height / 2 - Math.Max(0, offsetY);
                            if (!double.IsFinite(sx) || !double.IsFinite(sy) || Math.Abs(sx) > 1e9 || Math.Abs(sy) > 1e9) throw new InvalidDataException("鱼眼源采样坐标无效或过大。");
                            if (sx < 0 || sy < 0 || sx >= width - Math.Abs((long)offsetX) || sy >= height - Math.Abs((long)offsetY)) return null;
                            return new Point(sx, sy);
                        }
                        double radial = 1 + k[0] * r2 + k[1] * r2 * r2 + k[4] * r2 * r2 * r2;
                        return new Point(fx * (x * radial + 2 * k[2] * x * y + k[3] * (r2 + 2 * x * x)) + pcx, fy * (y * radial + k[2] * (r2 + 2 * y * y) + 2 * k[3] * x * y) + pcy);
                    });
                }
                break;
            default: throw new NotSupportedException("此类型不支持空间网格预览。");
        }
        CalibrationSpatialPreview result = new() { Width = Math.Max(width, outputWidth), Height = Math.Max(height, outputHeight), Series = series, ReferenceGrid = Grid(outputWidth, outputHeight, p => p, outputWidth, outputHeight), Note = fish ? "显示鱼眼输出网格对应的连续源采样坐标，坐标以中心平移后的裁剪源视图为参考；仅为参数示意。未模拟取整、插值、翻转或真实图像内容。" : "显示输出网格对应的连续源采样坐标；仅为参数示意，不代表相机图像校正结果。未模拟像素取整、边界填充、插值、翻转或裁剪。" };
        return result;
    }

    private static float CheckedFloat(double value) { float result = (float)value; if (!float.IsFinite(result)) throw new InvalidDataException("参数超过浮点范围。"); return result; }
    private static double Polynomial(double[] coefficients, double radius)
    {
        double value = 0; for (int i = coefficients.Length - 1; i >= 0; i--) value = value * radius + coefficients[i];
        if (!double.IsFinite(value) || Math.Abs(value) > 1e9) throw new InvalidDataException("径向位移过大，无法生成示意网格。"); return value;
    }

    private static IReadOnlyList<IReadOnlyList<Point>> Grid(int width, int height, Func<Point, Point?> map, int sourceWidth, int sourceHeight)
    {
        List<IReadOnlyList<Point>> lines = new();
        for (int direction = 0; direction < 2; direction++) for (int line = 0; line <= 10; line++)
        {
            List<Point> segment = new();
            for (int sample = 0; sample <= 32; sample++)
            {
                Point p = new((width - 1) * (direction == 0 ? line / 10.0 : sample / 32.0), (height - 1) * (direction == 0 ? sample / 32.0 : line / 10.0));
                Point? mapped = map(p);
                if (mapped.HasValue && (!double.IsFinite(mapped.Value.X) || !double.IsFinite(mapped.Value.Y) || Math.Abs(mapped.Value.X) > 1e9 || Math.Abs(mapped.Value.Y) > 1e9)) throw new InvalidDataException("源采样坐标无效或超出预览范围。");
                if (!mapped.HasValue || mapped.Value.X < 0 || mapped.Value.Y < 0 || mapped.Value.X >= sourceWidth || mapped.Value.Y >= sourceHeight)
                { if (segment.Count >= 2) lines.Add(segment); segment = new(); }
                else segment.Add(mapped.Value);
            }
            if (segment.Count >= 2) lines.Add(segment);
        }
        return lines;
    }
}
