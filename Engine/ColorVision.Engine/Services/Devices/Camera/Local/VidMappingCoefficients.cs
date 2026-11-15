using Newtonsoft.Json.Linq;
using System;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

internal readonly record struct VidMappingCoefficients(double A, double B, double C, double D)
{
    private static VidMappingCoefficients Read(JToken node)
    {
        double Number(string name) => node.Value<double?>(name) ?? throw new FormatException($"缺少 VID 系数 {name}。");
        return new(Number("a"), Number("b"), Number("c"), Number("d"));
    }

    private double Evaluate(double position)
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
}
