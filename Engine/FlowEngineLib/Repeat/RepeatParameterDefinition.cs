using System;
using System.Collections.Generic;
using System.Globalization;
using ST.Library.UI;

namespace FlowEngineLib.Repeat;

/// <summary>Explicitly declares the numeric execution parameters a node allows a repeat region to override.</summary>
public interface IRepeatParameterNode
{
    IReadOnlyList<RepeatParameterDefinition> GetRepeatParameters();
}

public sealed class RepeatParameterDefinition
{
    public string Key { get; }
    private readonly string displayName;
    private readonly string unit;
    private readonly string unavailableReason;
    public string DisplayName => Lang.Get(displayName);
    public string Unit => unit == "次" ? Lang.Get(unit) : unit;
    public double DefaultValue { get; }
    public bool IsInteger { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public string UnavailableReason => string.IsNullOrEmpty(unavailableReason) ? unavailableReason : Lang.Get(unavailableReason);
    private readonly Func<double, string> validateValue;

    public RepeatParameterDefinition(string key, string displayName, string unit, double defaultValue, bool isInteger = false,
        double minimum = double.NegativeInfinity, double maximum = double.PositiveInfinity, string unavailableReason = null, Func<double, string> validateValue = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        Key = key;
        this.displayName = displayName ?? key;
        this.unit = unit ?? string.Empty;
        DefaultValue = defaultValue;
        IsInteger = isInteger;
        Minimum = minimum;
        Maximum = maximum;
        this.unavailableReason = unavailableReason;
        this.validateValue = validateValue;
    }

    public bool TryValidate(double value, out string error)
    {
        if (!string.IsNullOrEmpty(UnavailableReason)) error = UnavailableReason;
        else if (!double.IsFinite(value)) error = string.Format(CultureInfo.CurrentCulture, Lang.Get("{0}必须是有限数值。"), DisplayName);
        else if (IsInteger && value != Math.Truncate(value)) error = string.Format(CultureInfo.CurrentCulture, Lang.Get("{0}必须是整数。"), DisplayName);
        else if (value < Minimum || value > Maximum) error = string.Format(CultureInfo.CurrentCulture, Lang.Get("{0}必须在 {1} 到 {2} {3} 范围内。"), DisplayName, Minimum, Maximum, Unit);
        else if (!string.IsNullOrEmpty(error = validateValue?.Invoke(value))) { error = Lang.Get(error); return false; }
        else { error = string.Empty; return true; }
        return false;
    }
}
