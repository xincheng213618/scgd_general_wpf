using ColorVision.Common.NativeMethods;
using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.Browser;

public enum TemplateBrowserIconSize { Small, Medium, Large, ExtraLarge }
public enum TemplateBrowserSort { SavedOrder, Name, Date }
public enum TemplateBrowserGroup { None, Name, Date }

public sealed class TemplateBrowserDisplayNameConverter : IValueConverter
{
    // Permit readable wrapping at filename separators without changing the stored or editable name.
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string name ? name.Replace("_", "_\u200B") : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

internal sealed class TemplateBrowserComparer(TemplateBrowserSort sort, bool descending, Func<TemplateBase, int> savedPosition) : IComparer
{
    public int Compare(object? x, object? y)
    {
        if (x is not TemplateBase left || y is not TemplateBase right) return 0;
        // Undated legacy records stay last in either direction.
        if (sort == TemplateBrowserSort.Date && left.Date.HasValue != right.Date.HasValue)
            return left.Date.HasValue ? -1 : 1;
        int result = sort switch
        {
            TemplateBrowserSort.Name => Shlwapi.CompareLogical(left.Key, right.Key),
            TemplateBrowserSort.Date => Nullable.Compare(left.Date, right.Date),
            _ => savedPosition(left).CompareTo(savedPosition(right))
        };
        if (result == 0) result = savedPosition(left).CompareTo(savedPosition(right));
        return descending ? -Math.Sign(result) : result;
    }
}

internal sealed record TemplateBrowserGroupKey(int Order, string Label);

internal sealed class TemplateBrowserGrouping(TemplateBrowserGroup group, DateTime today) : GroupDescription
{
    public override object GroupNameFromItem(object item, int level, CultureInfo culture)
    {
        var template = (TemplateBase)item;
        if (group == TemplateBrowserGroup.Name)
        {
            char first = char.ToUpperInvariant(template.Key.TrimStart().FirstOrDefault());
            return first switch
            {
                >= '0' and <= '9' => new TemplateBrowserGroupKey(0, "0 – 9"),
                >= 'A' and <= 'H' => new TemplateBrowserGroupKey(1, "A – H"),
                >= 'I' and <= 'P' => new TemplateBrowserGroupKey(2, "I – P"),
                >= 'Q' and <= 'Z' => new TemplateBrowserGroupKey(3, "Q – Z"),
                _ => Key(4, "中文及其他")
            };
        }
        if (template.Date is not DateTime date) return Key(10, "日期未知");
        date = date.Date;
        DateTime day = today.Date;
        DateTime week = day.AddDays(-((7 + (int)day.DayOfWeek - (int)culture.DateTimeFormat.FirstDayOfWeek) % 7));
        DateTime month = new(day.Year, day.Month, 1);
        if (date > day) return Key(0, "将来");
        if (date == day) return Key(1, "今天");
        if (date == day.AddDays(-1)) return Key(2, "昨天");
        if (date >= week) return Key(3, "本周早些时候");
        if (date >= week.AddDays(-7)) return Key(4, "上周");
        if (date >= month) return Key(5, "本月早些时候");
        if (date >= month.AddMonths(-1)) return Key(6, "上个月");
        if (date.Year == day.Year) return Key(7, "今年早些时候");
        return Key(8, "更早");
    }

    private static TemplateBrowserGroupKey Key(int order, string label) => new(order, EngineLocalization.Get(label));
}
