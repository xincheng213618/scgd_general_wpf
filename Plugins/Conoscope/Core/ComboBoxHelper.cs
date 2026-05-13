using System;
using System.Windows.Controls;

namespace Conoscope.Core
{
    internal static class ComboBoxHelper
    {
        public static void SelectItemByTag(ComboBox? comboBox, string tag)
        {
            if (comboBox == null)
            {
                return;
            }

            foreach (object rawItem in comboBox.Items)
            {
                if (rawItem is ComboBoxItem item
                    && string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        public static TEnum GetSelectedEnumByTag<TEnum>(ComboBox? comboBox, TEnum fallback)
            where TEnum : struct, Enum
        {
            if (comboBox?.SelectedItem is ComboBoxItem item
                && item.Tag?.ToString() is string enumTag
                && Enum.TryParse(enumTag, out TEnum value))
            {
                return value;
            }

            return fallback;
        }
    }
}