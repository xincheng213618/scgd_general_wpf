using ColorVision.Engine.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    public enum LumFourColorCorrectionMode
    {
        MatlabRgbw,
        SinglePoint,
    }

    public readonly record struct ColorCorrectionYxy(double Y, double CieX, double CieY);

    public readonly record struct ColorCorrectionSpectrumPoint(double Wavelength, double Value);

    public sealed record ColorCorrectionMeasurement(
        ColorCorrectionYxy Camera,
        ColorCorrectionYxy Reference,
        IReadOnlyList<ColorCorrectionSpectrumPoint>? Spectrum = null);

    public sealed record LumFourColorCorrectionMeasurements(
        ColorCorrectionMeasurement Red,
        ColorCorrectionMeasurement Green,
        ColorCorrectionMeasurement Blue,
        ColorCorrectionMeasurement White);

    public enum LumFourColorCorrectionTarget
    {
        SinglePoint,
        Red,
        Green,
        Blue,
        White
    }

    public static class LumFourColorCorrectionCalculator
    {
        public static CVRawManualCieConfig CorrectSinglePoint(CVRawManualCieConfig source, ColorCorrectionMeasurement measurement)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(measurement);

            double[] sourceMatrix = GetMatrix(source);
            double[] cameraRgb = Solve(sourceMatrix, ToXyz(measurement.Camera, "相机测量值"), 3, "原四色校正矩阵");
            double[] referenceRgb = Solve(sourceMatrix, ToXyz(measurement.Reference, "光谱参考值"), 3, "原四色校正矩阵");
            double[] correctedMatrix = new double[9];

            for (int channel = 0; channel < 3; channel++)
            {
                if (cameraRgb[channel] == 0d)
                {
                    throw new InvalidOperationException($"单点修正无法计算：相机测量反解后的第 {channel + 1} 个原始通道为 0。");
                }

                double ratio = referenceRgb[channel] / cameraRgb[channel];
                EnsureFinite(ratio, "单点修正通道比例");
                for (int row = 0; row < 3; row++)
                {
                    double coefficient = sourceMatrix[row * 3 + channel] * ratio;
                    EnsureFinite(coefficient, "单点修正矩阵系数");
                    correctedMatrix[row * 3 + channel] = coefficient;
                }
            }

            return CloneWithMatrix(source, correctedMatrix);
        }

        public static CVRawManualCieConfig CorrectFourColor(CVRawManualCieConfig source, LumFourColorCorrectionMeasurements measurements)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(measurements);

            double[] sourceMatrix = GetMatrix(source);
            ColorCorrectionMeasurement[] samples = [measurements.Red, measurements.Green, measurements.Blue, measurements.White];
            double[][] cameraRgb = new double[samples.Length][];
            double[,] equations = new double[9, 9];
            double[] rightHandSide = new double[9];

            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
            {
                ColorCorrectionMeasurement sample = samples[sampleIndex]
                    ?? throw new ArgumentException($"第 {sampleIndex + 1} 个 RGBW 测量值为空。", nameof(measurements));
                cameraRgb[sampleIndex] = Solve(sourceMatrix, ToXyz(sample.Camera, $"第 {sampleIndex + 1} 个相机测量值"), 3, "原四色校正矩阵");

                ColorCorrectionYxy reference = sample.Reference;
                ValidateYxy(reference, $"第 {sampleIndex + 1} 个光谱参考值");
                double xOverY = reference.CieX / reference.CieY;
                double zOverY = (1d - reference.CieX - reference.CieY) / reference.CieY;
                EnsureFinite(xOverY, "参考色度 x/y");
                EnsureFinite(zOverY, "参考色度 z/y");

                int xRow = sampleIndex * 2;
                int zRow = xRow + 1;
                for (int channel = 0; channel < 3; channel++)
                {
                    double raw = cameraRgb[sampleIndex][channel];
                    equations[xRow, channel] = raw;
                    equations[xRow, channel + 3] = -raw * xOverY;
                    equations[zRow, channel + 3] = -raw * zOverY;
                    equations[zRow, channel + 6] = raw;
                }
            }

            for (int channel = 0; channel < 3; channel++)
            {
                equations[8, channel + 3] = cameraRgb[3][channel];
            }
            rightHandSide[8] = samples[3].Reference.Y;

            double[] correctedMatrix = Solve(equations, rightHandSide, "四色修正方程");
            return CloneWithMatrix(source, correctedMatrix);
        }

        public static string SerializeCalibrationFile(CVRawManualCieConfig calibration)
        {
            ArgumentNullException.ThrowIfNull(calibration);
            double[] matrix = GetMatrix(calibration);
            double[] normalization =
            {
                calibration.Gain_x, calibration.Gain_y, calibration.Gain_z,
                calibration.Texp_x, calibration.Texp_y, calibration.Texp_z
            };
            foreach (double value in normalization)
            {
                EnsureFinite(value, "四色校正文件归一化参数");
            }

            JObject root = new()
            {
                [nameof(calibration.Gain_x)] = calibration.Gain_x,
                [nameof(calibration.Gain_y)] = calibration.Gain_y,
                [nameof(calibration.Gain_z)] = calibration.Gain_z,
                [nameof(calibration.Texp_x)] = calibration.Texp_x,
                [nameof(calibration.Texp_y)] = calibration.Texp_y,
                [nameof(calibration.Texp_z)] = calibration.Texp_z,
                ["a"] = matrix[0],
                ["b"] = matrix[1],
                ["c"] = matrix[2],
                ["d"] = matrix[3],
                ["e"] = matrix[4],
                ["f"] = matrix[5],
                ["g"] = matrix[6],
                ["h"] = matrix[7],
                ["i"] = matrix[8]
            };
            return root.ToString(Formatting.Indented);
        }

        private static double[] ToXyz(ColorCorrectionYxy value, string name)
        {
            ValidateYxy(value, name);
            double x = value.Y * value.CieX / value.CieY;
            double z = value.Y * (1d - value.CieX - value.CieY) / value.CieY;
            EnsureFinite(x, $"{name}换算的 X");
            EnsureFinite(z, $"{name}换算的 Z");
            return [x, value.Y, z];
        }

        private static void ValidateYxy(ColorCorrectionYxy value, string name)
        {
            EnsureFinite(value.Y, $"{name} Y");
            EnsureFinite(value.CieX, $"{name} CIE x");
            EnsureFinite(value.CieY, $"{name} CIE y");
            if (value.CieY == 0d)
            {
                throw new InvalidOperationException($"{name}的 CIE y 不能为 0。");
            }
        }

        private static double[] GetMatrix(CVRawManualCieConfig calibration)
        {
            double[] matrix =
            {
                calibration.A, calibration.B, calibration.C,
                calibration.D, calibration.E, calibration.F,
                calibration.G, calibration.H, calibration.I
            };
            foreach (double coefficient in matrix)
            {
                EnsureFinite(coefficient, "四色校正矩阵系数");
            }
            return matrix;
        }

        private static CVRawManualCieConfig CloneWithMatrix(CVRawManualCieConfig source, double[] matrix) => new()
        {
            Gain_x = source.Gain_x,
            Gain_y = source.Gain_y,
            Gain_z = source.Gain_z,
            Texp_x = source.Texp_x,
            Texp_y = source.Texp_y,
            Texp_z = source.Texp_z,
            A = matrix[0],
            B = matrix[1],
            C = matrix[2],
            D = matrix[3],
            E = matrix[4],
            F = matrix[5],
            G = matrix[6],
            H = matrix[7],
            I = matrix[8]
        };

        private static double[] Solve(double[] coefficients, double[] rightHandSide, int size, string name)
        {
            double[,] matrix = new double[size, size];
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    matrix[row, column] = coefficients[row * size + column];
                }
            }
            return Solve(matrix, rightHandSide, name);
        }

        private static double[] Solve(double[,] coefficients, double[] rightHandSide, string name)
        {
            int size = rightHandSide.Length;
            if (coefficients.GetLength(0) != size || coefficients.GetLength(1) != size)
            {
                throw new ArgumentException("线性方程矩阵尺寸不一致。", nameof(coefficients));
            }

            double[,] matrix = (double[,])coefficients.Clone();
            double[] result = (double[])rightHandSide.Clone();
            for (int column = 0; column < size; column++)
            {
                int pivotRow = column;
                double pivotMagnitude = Math.Abs(matrix[column, column]);
                for (int row = column + 1; row < size; row++)
                {
                    double magnitude = Math.Abs(matrix[row, column]);
                    if (magnitude > pivotMagnitude)
                    {
                        pivotMagnitude = magnitude;
                        pivotRow = row;
                    }
                }

                if (pivotMagnitude == 0d || !double.IsFinite(pivotMagnitude))
                {
                    throw new InvalidOperationException($"{name}不可逆，无法计算修正系数。");
                }

                if (pivotRow != column)
                {
                    for (int swapColumn = column; swapColumn < size; swapColumn++)
                    {
                        (matrix[column, swapColumn], matrix[pivotRow, swapColumn]) = (matrix[pivotRow, swapColumn], matrix[column, swapColumn]);
                    }
                    (result[column], result[pivotRow]) = (result[pivotRow], result[column]);
                }

                double pivot = matrix[column, column];
                for (int row = column + 1; row < size; row++)
                {
                    double factor = matrix[row, column] / pivot;
                    EnsureFinite(factor, $"{name}消元系数");
                    matrix[row, column] = 0d;
                    for (int targetColumn = column + 1; targetColumn < size; targetColumn++)
                    {
                        matrix[row, targetColumn] -= factor * matrix[column, targetColumn];
                        EnsureFinite(matrix[row, targetColumn], $"{name}中间结果");
                    }
                    result[row] -= factor * result[column];
                    EnsureFinite(result[row], $"{name}中间结果");
                }
            }

            for (int row = size - 1; row >= 0; row--)
            {
                double value = result[row];
                for (int column = row + 1; column < size; column++)
                {
                    value -= matrix[row, column] * result[column];
                }
                result[row] = value / matrix[row, row];
                EnsureFinite(result[row], $"{name}解");
            }

            return result;
        }

        private static void EnsureFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new InvalidOperationException($"{name}必须是有限数值。");
            }
        }
    }
}
