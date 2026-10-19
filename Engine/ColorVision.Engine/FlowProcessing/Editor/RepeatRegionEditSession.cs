using FlowEngineLib.Base;
using FlowEngineLib.Repeat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Editor;

/// <summary>A detached working copy. Only the outer editor transaction commits it to the canvas.</summary>
internal sealed class RepeatRegionEditSession
{
    internal const int MaximumEditableRounds = 100_000;
    public int Count { get; private set; }
    public IReadOnlyList<RepeatNodeEditModel> Nodes { get; }

    public RepeatRegionEditSession(IEnumerable<CVCommonNode> members, int count)
    {
        if (count < 1 || count > MaximumEditableRounds) throw new ArgumentOutOfRangeException(nameof(count));
        Count = count;
        Nodes = members.Select((node, index) => (node, index)).Where(item => item.node is IRepeatParameterNode)
            .Select(item => new RepeatNodeEditModel(item.node, item.index + 1, count)).ToArray();
    }

    public void Resize(int count)
    {
        if (count < 1 || count > MaximumEditableRounds) throw new ArgumentOutOfRangeException(nameof(count));
        foreach (var node in Nodes)
            foreach (var parameter in node.Parameters) parameter.Resize(count);
        Count = count;
    }

    public void RemoveRounds(IEnumerable<int> indices)
    {
        var removed = indices.Where(index => index >= 0 && index < Count).ToHashSet();
        if (removed.Count == 0) return;
        if (removed.Count == Count) throw new InvalidOperationException("至少保留一轮执行。");
        foreach (var node in Nodes)
            foreach (var parameter in node.Parameters)
                parameter.Replace(parameter.Values.Where((_, index) => !removed.Contains(index)).ToArray());
        Count -= removed.Count;
    }

    public bool TryValidate(out string error)
    {
        foreach (var node in Nodes)
        {
            if (!string.IsNullOrEmpty(node.LoadError)) { error = node.LoadError; return false; }
            foreach (var parameter in node.Parameters.Where(parameter => parameter.Enabled))
            {
                if (parameter.Values.Length != Count) { error = $"{node.Title} 的数组长度与执行次数不一致。"; return false; }
                for (int index = 0; index < Count; index++)
                    if (!parameter.Definition.TryValidate(parameter.Values[index], out string reason))
                    { error = $"{node.Title} · 第 {index + 1} 轮：{reason}"; return false; }
            }
        }
        error = string.Empty;
        return true;
    }

    public void Apply(string regionId, IReadOnlyList<CVCommonNode> members)
    {
        if (!TryValidate(out string error)) throw new InvalidOperationException(error);
        foreach (var member in members) member.SetRepeatRegion(regionId, Count, members.Count);
        foreach (var node in Nodes)
            node.Node.SetRepeatParameterValues(node.Parameters.Where(parameter => parameter.Enabled)
                .ToDictionary(parameter => parameter.Definition.Key, parameter => parameter.Values, StringComparer.Ordinal));
    }
}

internal sealed class RepeatNodeEditModel
{
    public CVCommonNode Node { get; }
    public string Title { get; }
    public IReadOnlyList<RepeatParameterEditSeries> Parameters { get; }
    public string LoadError { get; }

    public RepeatNodeEditModel(CVCommonNode node, int index, int count)
    {
        Node = node;
        Title = $"{index:00}  {node.Title}";
        var definitions = ((IRepeatParameterNode)node).GetRepeatParameters();
        Parameters = definitions.Select(definition => new RepeatParameterEditSeries(definition, count,
            node.RepeatParameterValues.TryGetValue(definition.Key, out double[]? values) ? values : null)).ToArray();
        string[] unknown = node.RepeatParameterValues.Keys.Except(definitions.Select(definition => definition.Key)).ToArray();
        LoadError = unknown.Length > 0 ? $"{Title} 含有当前版本不支持的参数：{string.Join("、", unknown)}。请取消原重复框后重新框选。" : string.Empty;
    }

    public override string ToString() => Title;
}

internal sealed class RepeatParameterEditSeries
{
    public RepeatParameterDefinition Definition { get; }
    public double[] Values { get; private set; }
    public bool Enabled { get; set; }
    public string Header => string.IsNullOrEmpty(Definition.Unit) ? Definition.DisplayName : $"{Definition.DisplayName} ({Definition.Unit})";

    public RepeatParameterEditSeries(RepeatParameterDefinition definition, int count, double[]? saved)
    {
        Definition = definition;
        Values = saved == null ? Enumerable.Repeat(definition.DefaultValue, count).ToArray() : (double[])saved.Clone();
        Enabled = saved != null || string.IsNullOrEmpty(definition.UnavailableReason);
        // An inconsistent saved series remains invalid until the user explicitly changes the round count.
    }

    public void Resize(int count)
    {
        double[] values = Enumerable.Repeat(Definition.DefaultValue, count).ToArray();
        Array.Copy(Values, values, Math.Min(Values.Length, count));
        Values = values;
    }

    public void Replace(double[] values) => Values = (double[])values.Clone();

    public void Fill(int first, int last, double start, double increment)
        => Array.Copy(GenerateFill(first, last, start, increment), 0, Values, first, last - first + 1);

    public double[] GenerateFill(int first, int last, double start, double increment)
    {
        if (!Enabled) throw new InvalidOperationException("请先启用此参数的逐轮编辑。");
        if (first < 0 || last < first || last >= Values.Length) throw new ArgumentOutOfRangeException(nameof(first), "轮次范围无效。");
        double[] generated = new double[last - first + 1];
        bool useDecimal = TryDecimal(start, out decimal decimalStart);
        useDecimal &= TryDecimal(increment, out decimal decimalIncrement);
        for (int index = 0; index < generated.Length; index++)
        {
            double value = start + index * increment;
            if (useDecimal)
            {
                // Calculate decimal steps before converting to the stored double, so crossing zero stays exact.
                try { value = ToDouble(decimalStart + index * decimalIncrement); }
                catch (OverflowException) { useDecimal = false; }
            }
            if (!Definition.TryValidate(value, out string error)) throw new InvalidOperationException($"第 {first + index + 1} 轮：{error}");
            generated[index] = value;
        }
        return generated;
    }

    // Round-trip text preserves input precision; values outside decimal's range or scale retain double arithmetic.
    private static bool TryDecimal(double value, out decimal result)
        => decimal.TryParse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            && ToDouble(result) == value;

    // Parsing avoids an extra rounding error in decimal's direct double conversion and normalizes decimal negative zero.
    private static double ToDouble(decimal value)
        => double.Parse(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public override string ToString() => Header;
}
