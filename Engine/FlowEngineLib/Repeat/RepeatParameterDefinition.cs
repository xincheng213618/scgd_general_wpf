using System;
using System.Collections.Generic;

namespace FlowEngineLib.Repeat;

/// <summary>Explicitly declares the numeric execution parameters a node allows a repeat region to override.</summary>
public interface IRepeatParameterNode
{
    IReadOnlyList<RepeatParameterDefinition> GetRepeatParameters();
}

public sealed class RepeatParameterDefinition
{
    public string Key { get; }
    public string DisplayName { get; }
    public string Unit { get; }
    public double DefaultValue { get; }
    public bool IsInteger { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public string UnavailableReason { get; }
    private readonly Func<double, string> validateValue;

    public RepeatParameterDefinition(string key, string displayName, string unit, double defaultValue, bool isInteger = false,
        double minimum = double.NegativeInfinity, double maximum = double.PositiveInfinity, string unavailableReason = null, Func<double, string> validateValue = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        Key = key;
        DisplayName = displayName ?? key;
        Unit = unit ?? string.Empty;
        DefaultValue = defaultValue;
        IsInteger = isInteger;
        Minimum = minimum;
        Maximum = maximum;
        UnavailableReason = unavailableReason;
        this.validateValue = validateValue;
    }

    public bool TryValidate(double value, out string error)
    {
        if (!string.IsNullOrEmpty(UnavailableReason)) error = UnavailableReason;
        else if (!double.IsFinite(value)) error = $"{DisplayName}必须是有限数值。";
        else if (IsInteger && value != Math.Truncate(value)) error = $"{DisplayName}必须是整数。";
        else if (value < Minimum || value > Maximum) error = $"{DisplayName}必须在 {Minimum} 到 {Maximum} {Unit} 范围内。";
        else if (!string.IsNullOrEmpty(error = validateValue?.Invoke(value))) return false;
        else { error = string.Empty; return true; }
        return false;
    }
}
