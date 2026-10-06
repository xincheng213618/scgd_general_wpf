using ColorVision.FileIO;
using cvColorVision;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ColorVision.Engine.Services.PhyCameras.Calibration.Creation;

public sealed record CalibrationColorSample(double[] RawPerExposure, double Y, double XChromaticity, double YChromaticity);
public sealed record CalibrationColorFit(JObject Json, double RelativeRmsError);

public static class CalibrationColorCreation
{
    public static CalibrationColorSample ReadSample(string path, double luminance, double x, double y, int radius, bool interleavedBgr, CancellationToken token = default)
    {
        if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        CalibrationRawAverage image = CalibrationMapCreation.Average(new[] { path }, token, interleavedBgr: interleavedBgr);
        if (CVFileUtil.ReadCIEFileHeader(path, out CVCIEFile header) < 0) throw new InvalidDataException("读取曝光元数据失败。");
        using (header)
        {
            double[] raw = new double[image.Channels]; int count = 0;
            for (int row = (int)Math.Max(0, image.Height / 2 - (long)radius); row < (int)Math.Min(image.Height, image.Height / 2 + (long)radius + 1); row++)
                for (int column = (int)Math.Max(0, image.Width / 2 - (long)radius); column < (int)Math.Min(image.Width, image.Width / 2 + (long)radius + 1); column++)
                {
                    int pixel = row * image.Width + column;
                    for (int channel = 0; channel < image.Channels; channel++)
                    {
                        double value = image.Values[channel * image.Width * image.Height + pixel];
                        if (value >= (image.BitsPerChannel == 8 ? 255 : 65535)) throw new InvalidDataException("色度 ROI 含饱和像素，请降低曝光后重新采集。");
                        raw[channel] += value;
                    }
                    count++;
                }
            for (int channel = 0; channel < raw.Length; channel++)
            {
                if (header.Exp == null || header.Exp.Length <= channel || !float.IsFinite(header.Exp[channel]) || header.Exp[channel] <= 0) throw new InvalidDataException("输入文件曝光必须是正数。");
                raw[channel] /= count * (double)header.Exp[channel];
            }
            return new(raw, luminance, x, y);
        }
    }

    public static CalibrationColorFit Fit(IReadOnlyList<CalibrationColorSample> samples, CalibrationType type, int bpp)
    {
        if (type is not (CalibrationType.Luminance or CalibrationType.LumOneColor or CalibrationType.LumFourColor or CalibrationType.LumMultiColor)) throw new ArgumentException("不支持的色度校正类型。");
        if (bpp is not (8 or 16) || samples.Count == 0) throw new ArgumentException("校正样本或位深无效。");
        int channels = type == CalibrationType.Luminance ? 1 : 3;
        double[,] design = new double[samples.Count, channels]; double[,] reference = new double[samples.Count, 3];
        for (int row = 0; row < samples.Count; row++)
        {
            CalibrationColorSample sample = samples[row];
            if (sample.RawPerExposure.Length != channels || !double.IsFinite(sample.Y) || sample.Y <= 0) throw new InvalidDataException("样本须有匹配通道及有限正数参考 Y。");
            if (channels == 3 && (!double.IsFinite(sample.XChromaticity) || !double.IsFinite(sample.YChromaticity) || sample.XChromaticity <= 0 || sample.YChromaticity <= 0 || sample.XChromaticity + sample.YChromaticity >= 1)) throw new InvalidDataException("色度样本须有有效的参考 x,y。");
            for (int column = 0; column < channels; column++)
            {
                double value = sample.RawPerExposure[column];
                if (!double.IsFinite(value) || value < 0 || (channels == 1 && value == 0)) throw new InvalidDataException(channels == 1 ? "原始亮度均值必须为正数。" : "原始通道均值必须为有限非负数。");
                design[row, column] = value;
            }
            reference[row, 1] = sample.Y;
            if (channels == 3)
            {
                reference[row, 0] = sample.Y * sample.XChromaticity / sample.YChromaticity;
                reference[row, 2] = sample.Y * (1 - sample.XChromaticity - sample.YChromaticity) / sample.YChromaticity;
            }
        }
        double[] coefficients = new double[type == CalibrationType.Luminance ? 1 : type == CalibrationType.LumOneColor ? 4 : 9];
        if (type == CalibrationType.Luminance) coefficients[0] = Solve(design, reference, 1, new[] { 0 })[0];
        else if (type == CalibrationType.LumOneColor)
        {
            double[] cross = Solve(design, reference, 0, new[] { 0, 2 }); coefficients[0] = cross[0]; coefficients[3] = cross[1];
            coefficients[1] = Solve(design, reference, 1, new[] { 1 })[0]; coefficients[2] = Solve(design, reference, 2, new[] { 2 })[0];
        }
        else for (int target = 0; target < 3; target++) Array.Copy(Solve(design, reference, target, new[] { 0, 1, 2 }), 0, coefficients, target * 3, 3);
        JObject json = new() { ["bpp"] = bpp, ["Texp_x"] = 1.0, ["Texp_y"] = 1.0, ["Texp_z"] = 1.0, ["Gain_x"] = 1.0, ["Gain_y"] = 1.0, ["Gain_z"] = 1.0 };
        if (type == CalibrationType.LumMultiColor) { json["pa"] = new JArray(coefficients); json["Gain"] = new JArray(1.0, 1.0, 1.0); }
        else for (int i = 0; i < coefficients.Length; i++) json[((char)('a' + i)).ToString()] = coefficients[i];
        double error = 0, energy = 0;
        for (int row = 0; row < samples.Count; row++)
            for (int output = 0; output < (channels == 1 ? 1 : 3); output++)
            {
                double actual = channels == 1 ? reference[row, 1] : reference[row, output];
                double prediction = type == CalibrationType.Luminance ? coefficients[0] * design[row, 0] : type == CalibrationType.LumOneColor ? output == 0 ? coefficients[0] * design[row, 0] + coefficients[3] * design[row, 2] : coefficients[output] * design[row, output] : coefficients[output * 3] * design[row, 0] + coefficients[output * 3 + 1] * design[row, 1] + coefficients[output * 3 + 2] * design[row, 2];
                error += (prediction - actual) * (prediction - actual); energy += actual * actual;
            }
        double relativeRms = Math.Sqrt(error / energy);
        if (!double.IsFinite(relativeRms)) throw new InvalidDataException("参考样本范围过大，拟合误差无效。");
        return new(json, relativeRms);
    }

    // Scaled modified Gram-Schmidt QR avoids squaring the condition number as normal equations do.
    private static double[] Solve(double[,] source, double[,] target, int output, int[] columns)
    {
        int rows = source.GetLength(0), count = columns.Length;
        if (rows < count) throw new InvalidDataException($"至少需要 {count} 个线性独立样本。");
        double[,] q = new double[rows, count], r = new double[count, count]; double[] scale = new double[count], projected = new double[count];
        for (int column = 0; column < count; column++)
        {
            for (int row = 0; row < rows; row++) scale[column] = Math.Max(scale[column], Math.Abs(source[row, columns[column]]));
            if (scale[column] == 0) throw new InvalidDataException("拟合所需通道没有有效响应，请增加不同颜色的参考样本。");
            for (int row = 0; row < rows; row++) q[row, column] = source[row, columns[column]] / scale[column];
            for (int previous = 0; previous < column; previous++)
            {
                for (int row = 0; row < rows; row++) r[previous, column] += q[row, previous] * q[row, column];
                for (int row = 0; row < rows; row++) q[row, column] -= r[previous, column] * q[row, previous];
            }
            for (int row = 0; row < rows; row++) r[column, column] += q[row, column] * q[row, column];
            r[column, column] = Math.Sqrt(r[column, column]);
            if (!double.IsFinite(r[column, column]) || r[column, column] < 1e-6 * Math.Sqrt(rows)) throw new InvalidDataException("样本通道接近线性相关，请增加不同颜色的参考样本。");
            for (int row = 0; row < rows; row++) { q[row, column] /= r[column, column]; projected[column] += q[row, column] * target[row, output]; }
        }
        double[] answer = new double[count];
        for (int column = count - 1; column >= 0; column--) { answer[column] = projected[column]; for (int i = column + 1; i < count; i++) answer[column] -= r[column, i] * answer[i]; answer[column] /= r[column, column]; }
        for (int column = 0; column < count; column++) { answer[column] /= scale[column]; if (!double.IsFinite(answer[column]) || !float.IsFinite((float)answer[column])) throw new InvalidDataException("拟合系数超出有效范围。"); }
        return answer;
    }
}


