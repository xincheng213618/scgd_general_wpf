using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using FlowEngineLib.Repeat;

namespace FlowEngineLib.Base;

public partial class CVCommonNode
{
    private readonly object repeatParametersSync = new();
    private Dictionary<string, double[]> repeatParameterValues = new(StringComparer.Ordinal);

    /// <summary>Returns an independent copy, so editing UI buffers cannot change saved or running parameters.</summary>
    [Browsable(false)]
    public IReadOnlyDictionary<string, double[]> RepeatParameterValues
    {
        get
        {
            lock (repeatParametersSync)
                return new ReadOnlyDictionary<string, double[]>(CopyRepeatParameters(repeatParameterValues));
        }
    }

    public void SetRepeatParameterValues(IReadOnlyDictionary<string, double[]> values)
    {
        Dictionary<string, double[]> copy = CopyRepeatParameters(values);
        lock (repeatParametersSync) repeatParameterValues = copy;
        OnPropertyChanged(nameof(RepeatParameterValues));
    }

    private static Dictionary<string, double[]> CopyRepeatParameters(IReadOnlyDictionary<string, double[]> values)
    {
        var copy = new Dictionary<string, double[]>(StringComparer.Ordinal);
        if (values != null)
            foreach (KeyValuePair<string, double[]> entry in values)
                copy.Add(entry.Key, entry.Value == null ? null : (double[])entry.Value.Clone());
        return copy;
    }

    /// <summary>Resolves an immutable per-run value without changing the node's configured default.</summary>
    public double ResolveRepeatParameter(CVStartCFC action, string key, double fallback)
    {
        if (action?.RepeatRun != null && action.RepeatRun.TryResolveParameter(this, action, key, out double value)) return value;
        return fallback;
    }

    /// <summary>
    /// Claims a single-node region for a device batch and returns a detached copy of its validated, frozen arrays.
    /// The same action instance must report successful batch completion before its output is forwarded.
    /// </summary>
    public bool TryGetSingleNodeRepeatParameters(CVStartCFC action, out int count, out IReadOnlyDictionary<string, double[]> values)
    {
        count = 0;
        values = null;
        return action?.RepeatRun != null && action.RepeatRun.TryGetBatchParameters(this, action, out count, out values);
    }

    /// <summary>Marks a claimed batch successful; normal output forwarding completes the region exactly once.</summary>
    public void CompleteRepeatBatch(CVStartCFC action, int count)
    {
        if (action?.RepeatRun == null) throw new InvalidOperationException("本次执行不属于可批量执行的重复框。");
        action.RepeatRun.CompleteBatch(this, action, count);
    }

    internal bool TryCaptureRepeatParameters(int count, out IReadOnlyDictionary<string, double[]> values, out string error)
    {
        values = RepeatParameterValues;
        error = string.Empty;
        if (values.Count == 0) return true;
        if (this is not IRepeatParameterNode provider)
        {
            error = $"节点“{Title}”不支持重复参数，请重新编辑重复框。";
            return false;
        }
        var definitions = new Dictionary<string, RepeatParameterDefinition>(StringComparer.Ordinal);
        foreach (RepeatParameterDefinition definition in provider.GetRepeatParameters())
        {
            if (definition == null || !definitions.TryAdd(definition.Key, definition))
            {
                error = $"节点“{Title}”的重复参数声明无效。";
                return false;
            }
        }
        foreach (KeyValuePair<string, double[]> entry in values)
        {
            if (!definitions.TryGetValue(entry.Key, out RepeatParameterDefinition definition))
            {
                error = $"节点“{Title}”不允许替换参数“{entry.Key}”，请重新编辑重复框。";
                return false;
            }
            if (entry.Value == null || entry.Value.Length != count)
            {
                error = $"节点“{Title}”的{definition.DisplayName}需要 {count} 个值，请重新编辑重复框。";
                return false;
            }
            for (int index = 0; index < entry.Value.Length; index++)
            {
                if (!definition.TryValidate(entry.Value[index], out string validationError))
                {
                    error = $"节点“{Title}”第 {index + 1} 次：{validationError}";
                    return false;
                }
            }
        }
        return true;
    }
}
