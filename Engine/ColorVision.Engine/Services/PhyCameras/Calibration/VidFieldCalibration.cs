using ColorVision.Engine.Services.PhyCameras.Calibration.Editing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Services.PhyCameras.Calibration;

internal readonly record struct VidMappingCoefficients(double A, double B, double C, double D)
{
    internal static VidMappingCoefficients Read(JToken node)
    {
        double Number(string name) => node.Value<double?>(name) ?? throw new FormatException($"缺少 VID 系数 {name}。");
        return new(Number("a"), Number("b"), Number("c"), Number("d"));
    }

    internal double Evaluate(double position)
    {
        double result = (position + A) / (B * position + C) + D;
        if (!double.IsFinite(result)) throw new FormatException("VID 映射结果不是有限值。");
        return result;
    }

    internal static double Evaluate(JObject mapping, double position)
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

    // y(p + offset) keeps the V4 form: (p + A + offset) / (B*p + C + B*offset) + D.
    internal VidMappingCoefficients ApplyPositionOffset(double offset) => new(A + offset, B, C + B * offset, D);

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

internal enum VidCorrectionModel
{
    Auto,
    // 1/VID' = 1/VID + k: an added lens or a vergence bias, matching 1/(1/d ± N).
    Diopter,
    // VID' = VID + k: the reference plane differs by a fixed distance.
    Distance,
    // VID'(p) = VID(p + k): the motor zero or the lens/sensor spacing shifted.
    Position,
}

internal readonly record struct VidCalibrationPoint(int Position, double Reference);

internal sealed record VidPointResult(int Position, double Reference, double Measured, double Corrected, double ResidualDiopters);

// Parameter units: Diopter in D (per metre), Distance in the mapping unit, Position in motor units.
internal sealed record VidModelFit(VidCorrectionModel Model, double Parameter, double RmsDiopters, string? Error);

internal sealed record VidFieldCalibration(VidCorrectionModel Model, double Parameter, IReadOnlyList<VidModelFit> Fits,
    IReadOnlyList<VidPointResult> Points, bool HasInfinityInRange, string Json)
{
    private static readonly VidCorrectionModel[] Models = [VidCorrectionModel.Diopter, VidCorrectionModel.Distance, VidCorrectionModel.Position];

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

    // Fits every model to the points and exports the requested one, or with Auto the one with the lowest
    // vergence RMS (ties prefer Diopter, then Distance, then Position). One point reproduces its reference exactly.
    internal static VidFieldCalibration Preview(string json, IReadOnlyList<VidCalibrationPoint> points, int minimum, int maximum,
        bool millimeters, VidCorrectionModel model = VidCorrectionModel.Auto)
    {
        if (points.Count == 0) throw new ArgumentException("请至少添加一个测点。");
        if (points.Any(point => !double.IsFinite(point.Reference) || point.Reference == 0)) throw new ArgumentException("标准 VID 必须为非零的有限数。");
        if (!Enum.IsDefined(model)) throw new ArgumentOutOfRangeException(nameof(model));
        var source = CalibrationJsonDocument.ParseObject(json);
        foreach (var segment in Segments(source)) VidMappingCoefficients.Read(segment.Node).Validate();
        double[] measured = points.Select(point => VidMappingCoefficients.Evaluate(source, point.Position)).ToArray();
        if (measured.Any(value => value == 0)) throw new FormatException("原映射在测点的 VID 为零，无法计算屈光度。");
        double scale = millimeters ? 1000 : 1;

        var fits = new List<VidModelFit>();
        var candidates = new Dictionary<VidCorrectionModel, (double Parameter, JObject Mapping, VidPointResult[] Points)>();
        foreach (var candidate in Models)
        {
            try
            {
                double parameter = Fit(source, points, measured, candidate);
                var mapping = Export(source, candidate, parameter);
                var results = new VidPointResult[points.Count];
                for (int i = 0; i < points.Count; i++)
                {
                    double expected = candidate switch
                    {
                        VidCorrectionModel.Diopter => 1 / (1 / measured[i] + parameter),
                        VidCorrectionModel.Distance => measured[i] + parameter,
                        _ => VidMappingCoefficients.Evaluate(source, points[i].Position + parameter),
                    };
                    double corrected = VidMappingCoefficients.Evaluate(mapping, points[i].Position);
                    if (!double.IsFinite(expected) || Math.Abs(corrected - expected) > Math.Max(1, Math.Abs(expected)) * 1e-9)
                        throw new FormatException("导出系数无法准确重现校正模型。");
                    results[i] = new(points[i].Position, points[i].Reference, measured[i], corrected, (1 / points[i].Reference - 1 / corrected) * scale);
                }
                double rms = Math.Sqrt(results.Average(result => result.ResidualDiopters * result.ResidualDiopters));
                double shown = candidate == VidCorrectionModel.Diopter ? parameter * scale : parameter;
                if (!double.IsFinite(rms) || !double.IsFinite(shown))
                    throw new FormatException("校正量或屈光度残差超出有效范围。");
                fits.Add(new(candidate, shown, rms, null));
                candidates[candidate] = (shown, mapping, results);
            }
            catch (Exception error) when (error is FormatException or ArithmeticException or InvalidOperationException)
            {
                fits.Add(new(candidate, double.NaN, double.NaN, error.Message));
            }
        }

        VidCorrectionModel chosen = model;
        if (model == VidCorrectionModel.Auto)
        {
            VidModelFit? best = null;
            foreach (var fit in fits.Where(fit => fit.Error == null))
                if (best == null || fit.RmsDiopters < best.RmsDiopters - 1e-9) best = fit;
            chosen = best?.Model ?? throw new FormatException(fits[0].Error ?? "所有校正模型均无法拟合。");
        }
        if (!candidates.TryGetValue(chosen, out var selected))
            throw new FormatException(fits.First(fit => fit.Model == chosen).Error);

        bool hasInfinityInRange = false;
        foreach (var segment in Segments(selected.Mapping))
        {
            long first = Math.Max(minimum, segment.Minimum), last = Math.Min(maximum, segment.Maximum);
            hasInfinityInRange |= first <= last && VidMappingCoefficients.Read(segment.Node).HasPoleIn((int)first, (int)last);
        }
        return new(chosen, selected.Parameter, fits, selected.Points, hasInfinityInRange, selected.Mapping.ToString(Formatting.Indented));
    }

    // Diopter and Distance use their least-squares means; Position minimizes the vergence residuals.
    private static double Fit(JObject source, IReadOnlyList<VidCalibrationPoint> points, double[] measured, VidCorrectionModel model)
    {
        double parameter = model switch
        {
            VidCorrectionModel.Diopter => points.Select((point, i) => 1 / point.Reference - 1 / measured[i]).Average(),
            VidCorrectionModel.Distance => points.Select((point, i) => point.Reference - measured[i]).Average(),
            _ => FitPositionOffset(source, points),
        };
        if (!double.IsFinite(parameter)) throw new FormatException("校正量超出有效范围。");
        return parameter;
    }

    private static double FitPositionOffset(JObject source, IReadOnlyList<VidCalibrationPoint> points)
    {
        double Residual(VidCalibrationPoint point, double offset)
        {
            try { return 1 / point.Reference - 1 / VidMappingCoefficients.Evaluate(source, point.Position + offset); }
            catch (FormatException) { return double.NaN; }
        }
        double Cost(double offset) => points.Sum(point => Math.Pow(Residual(point, offset), 2));

        // Damped Gauss-Newton on a smooth linear-fractional curve; a step that does not lower the cost is halved.
        double offset = 0, cost = Cost(0);
        if (!double.IsFinite(cost)) throw new FormatException("原映射在测点附近无效，无法拟合电机位置偏移。");
        for (int iteration = 0; iteration < 100 && cost > 0; iteration++)
        {
            double h = Math.Max(1e-3, Math.Abs(offset) * 1e-7), gradient = 0, curvature = 0;
            foreach (var point in points)
            {
                double slope = (Residual(point, offset + h) - Residual(point, offset - h)) / (2 * h);
                gradient += Residual(point, offset) * slope;
                curvature += slope * slope;
            }
            if (!double.IsFinite(gradient) || !double.IsFinite(curvature) || curvature == 0) break;
            double step = -gradient / curvature, next = offset + step, nextCost = Cost(next);
            for (int halving = 0; !(nextCost <= cost) && halving < 40; halving++)
            {
                step /= 2;
                next = offset + step;
                nextCost = Cost(next);
            }
            if (!(nextCost <= cost)) break;
            offset = next;
            cost = nextCost;
            if (Math.Abs(step) <= 1e-9 * Math.Max(1, Math.Abs(offset))) break;
        }
        return offset;
    }

    private static JObject Export(JObject source, VidCorrectionModel model, double parameter)
    {
        var mapping = (JObject)source.DeepClone();
        if (model == VidCorrectionModel.Position && mapping["VidNewVision"] is JObject split)
        {
            // Integer positions satisfy p + k > boundary exactly when p > floor(boundary - k).
            double boundary = Math.Floor((split.Value<int?>("positionValue") ?? throw new FormatException("缺少 positionValue。")) - parameter);
            if (boundary < int.MinValue || boundary > int.MaxValue) throw new FormatException("电机位置偏移使分段分界超出整数范围。");
            split["positionValue"] = (int)boundary;
        }
        foreach (var segment in Segments(mapping))
        {
            var original = VidMappingCoefficients.Read(segment.Node);
            var updated = model switch
            {
                VidCorrectionModel.Diopter => original.ApplyReciprocalOffset(parameter),
                VidCorrectionModel.Distance => original with { D = original.D + parameter },
                _ => original.ApplyPositionOffset(parameter),
            };
            updated.Validate();
            updated.Write(segment.Node);
        }
        return mapping;
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
