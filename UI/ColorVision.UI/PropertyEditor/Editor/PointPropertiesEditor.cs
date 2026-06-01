using ColorVision.UI;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace System.ComponentModel
{
    public abstract class NumericStructPropertiesEditor<TValue> : IPropertyEditor where TValue : struct
    {
        protected abstract string[] FieldToolTips { get; }

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var editorGrid = new Grid
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = PropertyEditorHelper.ControlMinWidth
            };

            var textBoxes = new List<TextBox>();
            for (int i = 0; i < FieldToolTips.Length; i++)
            {
                editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                TextBox textBox = CreateFieldTextBox(FieldToolTips[i], i == 0);
                Grid.SetColumn(textBox, i);
                editorGrid.Children.Add(textBox);
                textBoxes.Add(textBox);
            }
            dockPanel.Children.Add(editorGrid);

            bool isRefreshing = false;

            void Refresh()
            {
                if (property.GetValue(obj) is not TValue value)
                {
                    foreach (TextBox textBox in textBoxes)
                    {
                        textBox.Text = string.Empty;
                    }
                    return;
                }

                isRefreshing = true;
                for (int i = 0; i < textBoxes.Count; i++)
                {
                    textBoxes[i].Text = GetFieldText(value, i);
                }
                isRefreshing = false;
            }

            void Commit()
            {
                if (isRefreshing)
                {
                    return;
                }

                if (!TryCreateValue(textBoxes.Select(textBox => textBox.Text).ToArray(), out TValue newValue))
                {
                    Refresh();
                    return;
                }

                if (property.GetValue(obj) is TValue oldValue && oldValue.Equals(newValue))
                {
                    return;
                }

                property.SetValue(obj, newValue);
            }

            void TextBox_LostFocus(object sender, RoutedEventArgs e) => Commit();

            void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    Commit();
                }

                PropertyEditorHelper.TextBox_PreviewKeyDown(sender, e);
            }

            foreach (TextBox textBox in textBoxes)
            {
                textBox.LostFocus += TextBox_LostFocus;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            }

            if (obj is INotifyPropertyChanged notifyPropertyChanged)
            {
                void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
                {
                    if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == property.Name)
                    {
                        Refresh();
                    }
                }

                notifyPropertyChanged.PropertyChanged += OnPropertyChanged;
                dockPanel.Unloaded += (_, _) => notifyPropertyChanged.PropertyChanged -= OnPropertyChanged;
            }

            Refresh();
            return dockPanel;
        }

        protected abstract string GetFieldText(TValue value, int index);

        protected abstract bool TryCreateValue(IReadOnlyList<string> texts, out TValue value);

        private static TextBox CreateFieldTextBox(string toolTip, bool isFirst)
        {
            var textBox = new TextBox
            {
                Margin = new Thickness(isFirst ? 0 : 5, 0, 0, 0),
                Style = PropertyEditorHelper.TextBoxSmallStyle,
                ToolTip = toolTip
            };
            return textBox;
        }

        protected static string FormatDouble(double value)
        {
            return value.ToString("0.0################", CultureInfo.InvariantCulture);
        }

        protected static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        protected static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                || int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
        }
    }

    public class PointPropertiesEditor : NumericStructPropertiesEditor<Point>
    {
        private static readonly string[] Fields = new[] { "X", "Y" };

        static PointPropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<PointPropertiesEditor>(t => (Nullable.GetUnderlyingType(t) ?? t) == typeof(Point));
        }

        protected override string[] FieldToolTips => Fields;

        protected override string GetFieldText(Point value, int index)
        {
            return index switch
            {
                0 => FormatDouble(value.X),
                1 => FormatDouble(value.Y),
                _ => string.Empty
            };
        }

        protected override bool TryCreateValue(IReadOnlyList<string> texts, out Point value)
        {
            value = default;
            if (!TryParseDouble(texts[0], out double x) || !TryParseDouble(texts[1], out double y))
            {
                return false;
            }

            value = new Point(x, y);
            return true;
        }
    }

    public class RectPropertiesEditor : NumericStructPropertiesEditor<Rect>
    {
        private static readonly string[] Fields = new[] { "X", "Y", "Width", "Height" };

        static RectPropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<RectPropertiesEditor>(t => (Nullable.GetUnderlyingType(t) ?? t) == typeof(Rect));
        }

        protected override string[] FieldToolTips => Fields;

        protected override string GetFieldText(Rect value, int index)
        {
            return index switch
            {
                0 => FormatDouble(value.X),
                1 => FormatDouble(value.Y),
                2 => FormatDouble(value.Width),
                3 => FormatDouble(value.Height),
                _ => string.Empty
            };
        }

        protected override bool TryCreateValue(IReadOnlyList<string> texts, out Rect value)
        {
            value = default;
            if (!TryParseDouble(texts[0], out double x) ||
                !TryParseDouble(texts[1], out double y) ||
                !TryParseDouble(texts[2], out double width) ||
                !TryParseDouble(texts[3], out double height) ||
                width < 0 ||
                height < 0)
            {
                return false;
            }

            value = new Rect(x, y, width, height);
            return true;
        }
    }

    public class Int32RectPropertiesEditor : NumericStructPropertiesEditor<Int32Rect>
    {
        private static readonly string[] Fields = new[] { "X", "Y", "Width", "Height" };

        static Int32RectPropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<Int32RectPropertiesEditor>(t => (Nullable.GetUnderlyingType(t) ?? t) == typeof(Int32Rect));
        }

        protected override string[] FieldToolTips => Fields;

        protected override string GetFieldText(Int32Rect value, int index)
        {
            return index switch
            {
                0 => value.X.ToString(CultureInfo.InvariantCulture),
                1 => value.Y.ToString(CultureInfo.InvariantCulture),
                2 => value.Width.ToString(CultureInfo.InvariantCulture),
                3 => value.Height.ToString(CultureInfo.InvariantCulture),
                _ => string.Empty
            };
        }

        protected override bool TryCreateValue(IReadOnlyList<string> texts, out Int32Rect value)
        {
            value = default;
            if (!TryParseInt(texts[0], out int x) ||
                !TryParseInt(texts[1], out int y) ||
                !TryParseInt(texts[2], out int width) ||
                !TryParseInt(texts[3], out int height) ||
                width < 0 ||
                height < 0)
            {
                return false;
            }

            value = new Int32Rect(x, y, width, height);
            return true;
        }
    }

    public class SizePropertiesEditor : NumericStructPropertiesEditor<Size>
    {
        private static readonly string[] Fields = new[] { "Width", "Height" };

        static SizePropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<SizePropertiesEditor>(t => (Nullable.GetUnderlyingType(t) ?? t) == typeof(Size));
        }

        protected override string[] FieldToolTips => Fields;

        protected override string GetFieldText(Size value, int index)
        {
            return index switch
            {
                0 => FormatDouble(value.Width),
                1 => FormatDouble(value.Height),
                _ => string.Empty
            };
        }

        protected override bool TryCreateValue(IReadOnlyList<string> texts, out Size value)
        {
            value = default;
            if (!TryParseDouble(texts[0], out double width) ||
                !TryParseDouble(texts[1], out double height) ||
                width < 0 ||
                height < 0)
            {
                return false;
            }

            value = new Size(width, height);
            return true;
        }
    }

    public class ThicknessPropertiesEditor : NumericStructPropertiesEditor<Thickness>
    {
        private static readonly string[] Fields = new[] { "Left", "Top", "Right", "Bottom" };

        static ThicknessPropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<ThicknessPropertiesEditor>(t => (Nullable.GetUnderlyingType(t) ?? t) == typeof(Thickness));
        }

        protected override string[] FieldToolTips => Fields;

        protected override string GetFieldText(Thickness value, int index)
        {
            return index switch
            {
                0 => FormatDouble(value.Left),
                1 => FormatDouble(value.Top),
                2 => FormatDouble(value.Right),
                3 => FormatDouble(value.Bottom),
                _ => string.Empty
            };
        }

        protected override bool TryCreateValue(IReadOnlyList<string> texts, out Thickness value)
        {
            value = default;
            if (!TryParseDouble(texts[0], out double left) ||
                !TryParseDouble(texts[1], out double top) ||
                !TryParseDouble(texts[2], out double right) ||
                !TryParseDouble(texts[3], out double bottom))
            {
                return false;
            }

            value = new Thickness(left, top, right, bottom);
            return true;
        }
    }
}
