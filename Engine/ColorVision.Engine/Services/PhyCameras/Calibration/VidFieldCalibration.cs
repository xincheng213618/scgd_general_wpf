using ColorVision.Engine.Services.PhyCameras.Calibration.Editing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace ColorVision.Engine.Services.PhyCameras.Calibration;

internal readonly record struct VidMappingCoefficients(double A, double B, double C, double D)
{
    internal static VidMappingCoefficients Read(JToken node)
    {
        double Number(string name) => node.Value<double?>(name) ?? throw new FormatException($"缺少 VID 系数 {name}。");
        return new(Number("a"), Number("b"), Number("c"), Number("d"));
    }

    internal double Evaluate(int position)
    {
        double result = (position + A) / (B * position + C) + D;
        if (!double.IsFinite(result)) throw new FormatException("VID 映射结果不是有限值。");
        return result;
    }

    internal static double Evaluate(JObject mapping, int position)
    {
        JToken coefficients = mapping;
        if (mapping["VidNewVision"] is JObject split)
        {
            int boundary = split.Value<int?>("positionValue") ?? throw new FormatException("缺少 positionValue。");
            coefficients = split[position > boundary ? "longSighted" : "shortSighted"] ?? throw new FormatException("缺少 VID 分段系数。");
        }
        return Read(coefficients).Evaluate(position);
    }

    internal void Validate()
    {
        if (!double.IsFinite(A) || !double.IsFinite(B) || !double.IsFinite(C) || !double.IsFinite(D) || (B == 0 && C == 0))
            throw new FormatException("VID 映射系数无效。");
    }

    internal VidMappingCoefficients ApplyReciprocalOffset(double offset)
    {
        if (offset == 0) return this;

        // y = (u*p + v)/(B*p + C); y/(1 + offset*y) preserves the linear-fractional form.
        double u = 1 + B * D, v = A + C * D;
        double b = B + offset * u, c = C + offset * v;
        if (!double.IsFinite(u) || !double.IsFinite(v) || !double.IsFinite(b) || !double.IsFinite(c))
            throw new FormatException("VID 修正系数超出有效范围。");
        if (u == 0 && b == 0)
        {
            // A constant map can use an equal numerator and denominator with a pole outside int positions.
            const double outsideMotorRange = (double)int.MaxValue + 1;
            double value = v / c;
            if (!double.IsFinite(value) || value == 0) throw new FormatException("VID 修正系数无效。");
            double scale = 1 / value;
            return new(outsideMotorRange, scale, outsideMotorRange * scale, 0);
        }

        // Choose an output offset that gives a well-scaled, nonzero numerator slope for V4.
        double d = b != 0 && Math.Abs(b) > Math.Abs(u) ? (u == 0 || Math.Sign(u) == Math.Sign(b) ? -1 : 1) : 0;
        double slope = u - d * b;
        return new((v - d * c) / slope, b / slope, c / slope, d);
    }

    internal bool HasPoleIn(int minimum, int maximum) =>
        B != 0 && -C / B >= minimum && -C / B <= maximum;

    internal void Write(JObject node)
    {
        node["a"] = A;
        node["b"] = B;
        node["c"] = C;
        node["d"] = D;
    }
}

internal sealed record VidFieldCalibration(double Measured, double Reference, double OffsetDiopters, double Corrected, bool HasInfinityInRange, string Json)
{
    internal void SaveAs(string path, CalibrationJsonFileSnapshot source)
    {
        // Use UTF-8 JSON for the native reader and retain the original hash for collision checks.
        var utf8 = new CalibrationJsonFileSnapshot(source.Path, source.Json, new UTF8Encoding(false, true), source.Hash);
        CalibrationJsonFileStore.SaveAs(path, Json, utf8);
    }

    internal static void ValidateSource(string json)
    {
        foreach (var segment in Segments(CalibrationJsonDocument.ParseObject(json)))
            VidMappingCoefficients.Read(segment.Node).Validate();
    }

    internal static double ReadCurrent(string json, int position) =>
        VidMappingCoefficients.Evaluate(CalibrationJsonDocument.ParseObject(json), position);

    internal static VidFieldCalibration Preview(string json, int position, int minimum, int maximum, double reference, bool millimeters)
    {
        if (!double.IsFinite(reference) || reference == 0) throw new ArgumentException("标准 VID 必须为非零的有限数。");
        var mapping = CalibrationJsonDocument.ParseObject(json);
        double measured = VidMappingCoefficients.Evaluate(mapping, position);
        if (measured == 0) throw new FormatException("原映射在测量位置的 VID 为零，无法计算屈光度。");
        double offset = (measured - reference) / measured / reference;
        double offsetDiopters = offset * (millimeters ? 1000 : 1);
        if (!double.IsFinite(offsetDiopters)) throw new FormatException("屈光度偏差超出有效范围。");

        bool hasInfinityInRange = false;
        foreach (var segment in Segments(mapping))
        {
            var original = VidMappingCoefficients.Read(segment.Node);
            original.Validate();
            var updated = original.ApplyReciprocalOffset(offset);
            updated.Validate();
            long first = Math.Max(minimum, segment.Minimum), last = Math.Min(maximum, segment.Maximum);
            hasInfinityInRange |= first <= last && updated.HasPoleIn((int)first, (int)last);
            updated.Write(segment.Node);
        }

        double corrected = VidMappingCoefficients.Evaluate(mapping, position);
        if (Math.Abs(corrected - reference) > Math.Max(1, Math.Abs(reference)) * 1e-9)
            throw new FormatException("导出系数无法准确重现标准 VID。");
        return new(measured, reference, offsetDiopters, corrected, hasInfinityInRange, mapping.ToString(Formatting.Indented));
    }

    private static IEnumerable<(JObject Node, long Minimum, long Maximum)> Segments(JObject mapping)
    {
        if (mapping["VidNewVision"] is not JToken split || split.Type == JTokenType.Null)
        {
            yield return (mapping, int.MinValue, int.MaxValue);
            yield break;
        }
        if (split is not JObject || split["positionValue"] is not { Type: JTokenType.Integer } boundaryValue)
            throw new FormatException("VID 分段映射缺少整数分界位置。");
        if (split["shortSighted"] is not JObject near || split["longSighted"] is not JObject far)
            throw new FormatException("缺少 VID 分段系数。");
        int boundary = boundaryValue.Value<int>();
        yield return (near, int.MinValue, boundary);
        yield return (far, (long)boundary + 1, int.MaxValue);
    }
}
