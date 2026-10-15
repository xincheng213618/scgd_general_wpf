using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ColorVision.Engine.Services.PhyCameras.Calibration.Creation;

public sealed record CalibrationDistortionFit(JObject Json, double ReprojectionRmsPixels, int ImageCount);

public static class CalibrationDistortionCreation
{
    public static CalibrationDistortionFit Fit(IReadOnlyList<string> paths, int columns, int rows, double squareSize, CancellationToken token = default, IProgress<string>? progress = null)
    {
        if (columns < 3 || rows < 3 || columns > 100 || rows > 100 || !double.IsFinite(squareSize) || squareSize <= 0) throw new ArgumentException("棋盘内角点行列须为 3~100，格距须为正数。");
        if (paths.Count < 5) throw new InvalidDataException("至少需要五张不同姿态、覆盖画面各区域的棋盘图像。");
        List<IEnumerable<Point3f>> objectPoints = new(); List<IEnumerable<Point2f>> imagePoints = new();
        Size size = default; Point3f[] pattern = new Point3f[checked(columns * rows)];
        if ((float)squareSize <= 0 || !float.IsFinite((float)(Math.Max(columns, rows) * squareSize))) throw new ArgumentOutOfRangeException(nameof(squareSize), "格距超出有效范围。");
        for (int row = 0; row < rows; row++) for (int column = 0; column < columns; column++) pattern[row * columns + column] = new Point3f((float)(column * squareSize), (float)(row * squareSize), 0);
        foreach (string path in paths)
        {
            token.ThrowIfCancellationRequested();
            using Mat gray = Cv2.ImRead(path, ImreadModes.Grayscale);
            if (gray.Empty()) throw new InvalidDataException($"无法读取图像：{Path.GetFileName(path)}");
            if (size.Width == 0) size = gray.Size(); else if (gray.Size() != size) throw new InvalidDataException("所有棋盘图像尺寸必须相同。");
            if (!Cv2.FindChessboardCorners(gray, new Size(columns, rows), out Point2f[] corners, ChessboardFlags.AdaptiveThresh | ChessboardFlags.NormalizeImage)) throw new InvalidDataException($"没有找到完整棋盘内角点：{Path.GetFileName(path)}");
            Cv2.CornerSubPix(gray, corners, new Size(11, 11), new Size(-1, -1), new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.Count, 40, 0.001));
            objectPoints.Add(pattern); imagePoints.Add(corners); progress?.Report($"已检测棋盘 {imagePoints.Count}/{paths.Count}");
        }
        double[,] camera = new double[3, 3]; double[] distortion = new double[5];
        token.ThrowIfCancellationRequested();
        double rms = Cv2.CalibrateCamera(objectPoints, imagePoints, size, camera, distortion, out Vec3d[] _, out Vec3d[] _, CalibrationFlags.None);
        token.ThrowIfCancellationRequested();
        if (!double.IsFinite(rms) || camera[0, 0] <= 0 || camera[1, 1] <= 0) throw new InvalidDataException("棋盘拟合未得到有效相机内参。");
        JArray coefficients = new(); foreach (double value in camera) { if (!double.IsFinite(value) || !float.IsFinite((float)value)) throw new InvalidDataException("相机矩阵无效。"); coefficients.Add(value); }
        foreach (double value in distortion) if (!double.IsFinite(value) || !float.IsFinite((float)value)) throw new InvalidDataException("畸变系数无效。");
        JObject json = new() { ["w"] = size.Width, ["h"] = size.Height, ["s_w"] = size.Width, ["s_h"] = size.Height, ["useFisheye"] = false, ["alpha"] = 0.0, ["cameraMatrix"] = coefficients, ["distCoeffs"] = new JArray(distortion) };
        return new(json, rms, paths.Count);
    }
}
