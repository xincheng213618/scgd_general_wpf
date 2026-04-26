using ColorVision.UI;
using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.PropertyEditor
{
    /// <summary>
    /// Property editor for byte-based size values with GB input/display.
    /// </summary>
    public class FolderSizeBytesPropertiesEditor : System.ComponentModel.IPropertyEditor
    {
        private const long OneGb = 1024L * 1024L * 1024L;
        private static readonly long[] PresetsGb = new[] { 10L, 50L, 100L, 200L };

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var label = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(label);

            var editorPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5, 0, 0, 0)
            };

            var textBox = new TextBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                Style = PropertyEditorHelper.TextBoxSmallStyle,
                MinWidth = 110,
                VerticalContentAlignment = VerticalAlignment.Center,
                Text = ToGbText(ConvertToLong(property.GetValue(obj)))
            };
            textBox.MinWidth = 110;
            textBox.LostFocus += (_, _) => NormalizeValue(property, obj, textBox);
            textBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    NormalizeValue(property, obj, textBox);
                    PropertyEditorHelper.TextBox_PreviewKeyDown(s, e);
                }
            };
            editorPanel.Children.Add(textBox);

            editorPanel.Children.Add(new TextBlock
            {
                Text = "GB",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 10, 0)
            });

            foreach (var presetGb in PresetsGb)
            {
                var btn = new Button
                {
                    Content = $"{presetGb}G",
                    MinWidth = 44,
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(0, 0, 4, 0)
                };
                btn.Click += (_, _) =>
                {
                    var bytes = GbToBytes(presetGb);
                    SetPropertyValue(property, obj, bytes);
                    textBox.Text = ToGbText(bytes);
                };
                editorPanel.Children.Add(btn);
            }

            dockPanel.Children.Add(editorPanel);
            return dockPanel;
        }

        private static string ToGbText(long bytes)
        {
            var gb = bytes / (double)OneGb;
            return gb.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static void NormalizeValue(PropertyInfo property, object obj, TextBox textBox)
        {
            if (!double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var gbValue))
            {
                textBox.Text = ToGbText(ConvertToLong(property.GetValue(obj)));
                return;
            }

            if (gbValue <= 0)
            {
                gbValue = 1;
            }

            var bytes = GbToBytes(gbValue);
            SetPropertyValue(property, obj, bytes);
            textBox.Text = ToGbText(bytes);
        }

        private static long GbToBytes(double gbValue)
        {
            var bytes = gbValue * OneGb;
            if (bytes < 1)
            {
                return OneGb;
            }

            if (bytes > long.MaxValue)
            {
                return long.MaxValue;
            }

            return (long)Math.Round(bytes, MidpointRounding.AwayFromZero);
        }

        private static long ConvertToLong(object? value)
        {
            if (value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt64(value);
            }
            catch
            {
                return 0;
            }
        }

        private static void SetPropertyValue(PropertyInfo property, object obj, long value)
        {
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var converted = Convert.ChangeType(value, targetType);
            property.SetValue(obj, converted);
        }
    }
}