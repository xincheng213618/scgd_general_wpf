using System;
using System.Windows;
using System.Windows.Controls;

namespace Conoscope.Presentation.Helpers
{
    internal static class ComboBoxHelper
    {
        public static void SelectItemByTag(ComboBox? comboBox, string tag)
        {
            TrySelectItemByTag(comboBox, tag);
        }

        public static bool TrySelectItemByTag(ComboBox? comboBox, string tag, bool visibleOnly = false)
        {
            if (comboBox == null)
            {
                return false;
            }

            foreach (object rawItem in comboBox.Items)
            {
                if (rawItem is ComboBoxItem item
                    && (!visibleOnly || item.Visibility == Visibility.Visible)
                    && string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return true;
                }
            }

            return false;
        }

        public static void SetItemVisibilityByTag(ComboBox? comboBox, string tag, Visibility visibility)
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
                    item.Visibility = visibility;
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