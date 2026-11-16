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
        double result = EvaluateUnchecked(position);
        if (!double.IsFinite(result)) throw new FormatException("VID 映射结果不是有限值。");
        return result;
    }

    private double EvaluateUnchecked(double position) => (position + A) / (B * position + C) + D;

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

    internal static int? FindFirstPosition(JObject mapping, double vid, int minimum, int maximum)
    {
        if (mapping["VidNewVision"] is not JObject split) return Read(mapping).FindClosestPosition(vid, minimum, maximum);
        int boundary = split.Value<int?>("positionValue") ?? throw new FormatException("缺少 positionValue。");
        if (boundary >= minimum && FindOnBranch("shortSighted", minimum, Math.Min(boundary, maximum)) is int first)
            return first;
        return boundary < maximum ? FindOnBranch("longSighted", Math.Max(boundary + 1, minimum), maximum) : null;

        int? FindOnBranch(string name, int start, int end)
        {
            JToken coefficients = split[name] ?? throw new FormatException("缺少 VID 分段系数。");
            return Read(coefficients).FindClosestPosition(vid, start, end);
        }
    }

    private int? FindClosestPosition(double vid, int minimum, int maximum)
    {
        if (!double.IsFinite(A) || !double.IsFinite(B) || !double.IsFinite(C) || !double.IsFinite(D) || (B == 0 && C == 0))
            throw new FormatException("VID 映射系数无效。");

        double shift = vid - D;
        double numerator = shift * C - A;
        double denominator = 1 - shift * B;
        if (denominator == 0)
        {
            if (numerator != 0) return null;
            // A constant branch can only skip its undefined pole before the first position.
            if (EvaluateUnchecked(minimum) == vid) return minimum;
            if (minimum < maximum && EvaluateUnchecked(minimum + 1) == vid) return minimum + 1;
            return null;
        }

        double exact = numerator / denominator;
        if (!double.IsFinite(exact)) return null;
        // Roundoff in the inverse may put an exact physical endpoint just outside its branch.
        if (exact < minimum) return EvaluateUnchecked(minimum) == vid ? minimum : null;
        if (exact > maximum) return EvaluateUnchecked(maximum) == vid ? maximum : null;

        int lower = (int)Math.Floor(exact), upper = (int)Math.Ceiling(exact);
        double actualDivisor = B * exact + C;
        if (actualDivisor == 0) return null;
        bool positiveSide = actualDivisor > 0;
        double lowerError = PositionError(lower, vid, positiveSide);
        double upperError = PositionError(upper, vid, positiveSide);
        if (!double.IsFinite(Math.Min(lowerError, upperError))) return null;
        return lowerError <= upperError ? lower : upper;
    }

    private double PositionError(int position, double vid, bool positiveSide)
    {
        double divisor = B * position + C;
        if (divisor == 0 || (divisor > 0) != positiveSide) return double.PositiveInfinity;
        return Math.Abs(EvaluateUnchecked(position) - vid);
    }
}
