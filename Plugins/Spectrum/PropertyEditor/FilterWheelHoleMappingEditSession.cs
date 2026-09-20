using ColorVision.Common.MVVM;
using Spectrum.Configs;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Spectrum.PropertyEditor;

public sealed class FilterWheelHoleMappingRow : ViewModelBase
{
    public FilterWheelHoleMappingRow(FilterWheelHoleMap hole)
    {
        Hole = hole.Clone();
        _indexText = hole.HoleIndex.ToString(CultureInfo.CurrentCulture);
    }

    // Keep the complete mapping, including the undisplayed calibration group.
    public FilterWheelHoleMap Hole { get; }

    public string IndexText
    {
        get => _indexText;
        set { _indexText = value; OnPropertyChanged(); }
    }
    private string _indexText;
}

/// <summary>Detached edits; only a validated snapshot can be written back by the property editor.</summary>
public sealed class FilterWheelHoleMappingEditSession
{
    public FilterWheelHoleMappingEditSession(IEnumerable<FilterWheelHoleMap>? mapping)
    {
        Rows = new((mapping ?? []).Select(hole => new FilterWheelHoleMappingRow(hole)));
    }

    public ObservableCollection<FilterWheelHoleMappingRow> Rows { get; }

    public FilterWheelHoleMappingRow AddRow()
    {
        var used = new HashSet<int>();
        foreach (var row in Rows)
            if (int.TryParse(row.IndexText, out int index)) used.Add(index);

        int nextIndex = 0;
        while (used.Contains(nextIndex)) nextIndex++;
        var added = new FilterWheelHoleMappingRow(new FilterWheelHoleMap { HoleIndex = nextIndex });
        Rows.Add(added);
        return added;
    }

    public bool TryCreateMapping(out ObservableCollection<FilterWheelHoleMap>? mapping, out string error)
    {
        mapping = null;
        var candidate = new ObservableCollection<FilterWheelHoleMap>();
        var used = new HashSet<int>();
        for (int i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i];
            if (!int.TryParse(row.IndexText, out int index))
            {
                error = $"第 {i + 1} 行：孔位索引请输入有效整数（{int.MinValue} 至 {int.MaxValue}）。";
                return false;
            }
            if (!used.Add(index))
            {
                error = $"第 {i + 1} 行：孔位索引 {index} 重复，请使用不同的索引。";
                return false;
            }

            var hole = row.Hole.Clone();
            hole.HoleIndex = index;
            candidate.Add(hole);
        }

        mapping = candidate;
        error = string.Empty;
        return true;
    }
}
