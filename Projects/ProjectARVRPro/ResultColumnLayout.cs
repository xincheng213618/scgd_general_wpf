using System.Windows;
using System.Windows.Controls;

namespace ProjectARVRPro;

public static class ResultColumnLayout
{
    public static readonly DependencyProperty ColumnIdProperty = DependencyProperty.RegisterAttached(
        "ColumnId", typeof(string), typeof(ResultColumnLayout), new PropertyMetadata(null));

    public static string? GetColumnId(DependencyObject element) => (string?)element.GetValue(ColumnIdProperty);
    public static void SetColumnId(DependencyObject element, string value) => element.SetValue(ColumnIdProperty, value);

    internal static string[] Capture(GridViewColumnCollection columns) =>
        columns.Select(GetColumnId).OfType<string>().ToArray();

    internal static void Restore(GridViewColumnCollection columns, IReadOnlyList<string>? savedOrder)
    {
        if (savedOrder is not { Count: > 0 }) return;

        int position = 0;
        foreach (string id in savedOrder)
        {
            if (string.IsNullOrEmpty(id)) continue;
            // Columns already placed are skipped, so a repeated ID can't move the same column again.
            var column = columns.Skip(position).FirstOrDefault(candidate => GetColumnId(candidate) == id);
            if (column == null) continue;

            int currentIndex = columns.IndexOf(column);
            if (currentIndex != position) columns.Move(currentIndex, position);
            position++;
        }
    }
}
