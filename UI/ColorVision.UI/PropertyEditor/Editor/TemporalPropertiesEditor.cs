using ColorVision.UI;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace System.ComponentModel
{
    public class TemporalPropertiesEditor : IPropertyEditor
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string DateTimeOffsetFormat = "yyyy-MM-dd HH:mm:ss zzz";
        private const string TimeOnlyFormat = "HH:mm:ss";
        private static readonly string[] TimeOnlyFormats = new[] { "HH:mm", "HH:mm:ss", "HH:mm:ss.fff" };

        static TemporalPropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<TemporalPropertiesEditor>(IsSupportedType);
        }

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            FrameworkElement editor = propertyType == typeof(DateTime) || propertyType == typeof(DateOnly)
                ? CreateDateEditor(property, obj, propertyType)
                : CreateTextEditor(property, obj, propertyType);

            dockPanel.Children.Add(editor);
            return dockPanel;
        }

        private static bool IsSupportedType(Type type)
        {
            var propertyType = Nullable.GetUnderlyingType(type) ?? type;
            return propertyType == typeof(DateTime) ||
                   propertyType == typeof(DateOnly) ||
                   propertyType == typeof(TimeOnly) ||
                   propertyType == typeof(TimeSpan) ||
                   propertyType == typeof(DateTimeOffset);
        }

        private static StackPanel CreateDateEditor(PropertyInfo property, object obj, Type propertyType)
        {
            var editorPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5, 0, 0, 0)
            };

            var datePicker = new DatePicker
            {
                MinWidth = 120,
                SelectedDate = GetSelectedDate(property, obj, propertyType)
            };
            editorPanel.Children.Add(datePicker);

            TextBox? timeTextBox = null;
            if (propertyType == typeof(DateTime))
            {
                timeTextBox = CreateTemporalTextBox(GetDateTimeClockText(property, obj), 80);
                timeTextBox.Margin = new Thickness(5, 0, 0, 0);
                editorPanel.Children.Add(timeTextBox);
            }

            void CommitValue()
            {
                CommitDateValue(property, obj, propertyType, datePicker, timeTextBox);
            }

            datePicker.SelectedDateChanged += (_, __) => CommitValue();
            if (timeTextBox != null)
            {
                timeTextBox.LostFocus += (_, __) => CommitValue();
                timeTextBox.PreviewKeyDown += (_, eventArgs) => CommitOnEnter(timeTextBox, CommitValue, eventArgs);
            }

            return editorPanel;
        }

        private static TextBox CreateTextEditor(PropertyInfo property, object obj, Type propertyType)
        {
            var textBox = CreateTemporalTextBox(FormatTemporalValue(property.GetValue(obj), propertyType), PropertyEditorHelper.ControlMinWidth);

            void CommitValue()
            {
                CommitTextValue(property, obj, propertyType, textBox);
            }

            textBox.LostFocus += (_, __) => CommitValue();
            textBox.PreviewKeyDown += (_, eventArgs) => CommitOnEnter(textBox, CommitValue, eventArgs);
            return textBox;
        }

        private static TextBox CreateTemporalTextBox(string text, double minWidth)
        {
            return new TextBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = minWidth,
                Style = PropertyEditorHelper.TextBoxSmallStyle,
                Text = text
            };
        }

        private static DateTime? GetSelectedDate(PropertyInfo property, object obj, Type propertyType)
        {
            var value = property.GetValue(obj);
            if (propertyType == typeof(DateTime) && value is DateTime dateTime)
            {
                return dateTime.Date;
            }

            if (propertyType == typeof(DateOnly) && value is DateOnly dateOnly)
            {
                return dateOnly.ToDateTime(TimeOnly.MinValue);
            }

            return null;
        }

        private static string GetDateTimeClockText(PropertyInfo property, object obj)
        {
            return property.GetValue(obj) is DateTime dateTime
                ? dateTime.ToString(TimeOnlyFormat, CultureInfo.CurrentCulture)
                : TimeOnly.MinValue.ToString(TimeOnlyFormat, CultureInfo.CurrentCulture);
        }

        private static void CommitDateValue(PropertyInfo property, object obj, Type propertyType, DatePicker datePicker, TextBox? timeTextBox)
        {
            if (datePicker.SelectedDate == null)
            {
                if (CanSetNull(property))
                {
                    property.SetValue(obj, null);
                    return;
                }

                ResetDateEditor(property, obj, propertyType, datePicker, timeTextBox);
                return;
            }

            if (propertyType == typeof(DateOnly))
            {
                property.SetValue(obj, DateOnly.FromDateTime(datePicker.SelectedDate.Value));
                return;
            }

            var timeText = timeTextBox?.Text ?? string.Empty;
            if (!TryParseClockText(timeText, out var timeOfDay))
            {
                ResetDateEditor(property, obj, propertyType, datePicker, timeTextBox);
                return;
            }

            var dateTime = datePicker.SelectedDate.Value.Date + timeOfDay;
            if (property.GetValue(obj) is DateTime currentDateTime)
            {
                dateTime = DateTime.SpecifyKind(dateTime, currentDateTime.Kind);
            }

            property.SetValue(obj, dateTime);
            if (timeTextBox != null)
            {
                timeTextBox.Text = dateTime.ToString(TimeOnlyFormat, CultureInfo.CurrentCulture);
            }
        }

        private static void CommitTextValue(PropertyInfo property, object obj, Type propertyType, TextBox textBox)
        {
            var text = textBox.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                if (CanSetNull(property))
                {
                    property.SetValue(obj, null);
                    return;
                }

                textBox.Text = FormatTemporalValue(property.GetValue(obj), propertyType);
                return;
            }

            if (TryParseTemporalValue(propertyType, text, out var value))
            {
                property.SetValue(obj, value);
                textBox.Text = FormatTemporalValue(value, propertyType);
                return;
            }

            textBox.Text = FormatTemporalValue(property.GetValue(obj), propertyType);
        }

        private static bool TryParseTemporalValue(Type propertyType, string text, out object? value)
        {
            value = null;
            if (propertyType == typeof(TimeSpan) &&
                (TimeSpan.TryParse(text, CultureInfo.CurrentCulture, out var timeSpan) ||
                 TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out timeSpan)))
            {
                value = timeSpan;
                return true;
            }

            if (propertyType == typeof(TimeOnly) && TryParseTimeOnly(text, out var timeOnly))
            {
                value = timeOnly;
                return true;
            }

            if (propertyType == typeof(DateTimeOffset) &&
                DateTimeOffset.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var dateTimeOffset))
            {
                value = dateTimeOffset;
                return true;
            }

            return false;
        }

        private static bool TryParseClockText(string text, out TimeSpan timeOfDay)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                timeOfDay = TimeSpan.Zero;
                return true;
            }

            if (TryParseTimeOnly(text, out var timeOnly))
            {
                timeOfDay = timeOnly.ToTimeSpan();
                return true;
            }

            if (TimeSpan.TryParse(text, CultureInfo.CurrentCulture, out timeOfDay) &&
                timeOfDay >= TimeSpan.Zero && timeOfDay < TimeSpan.FromDays(1))
            {
                return true;
            }

            timeOfDay = TimeSpan.Zero;
            return false;
        }

        private static bool TryParseTimeOnly(string text, out TimeOnly value)
        {
            return TimeOnly.TryParse(text, out value) ||
                   TimeOnly.TryParseExact(text, TimeOnlyFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        private static string FormatTemporalValue(object? value, Type propertyType)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (propertyType == typeof(TimeSpan) && value is TimeSpan timeSpan)
            {
                return timeSpan.ToString("c", CultureInfo.InvariantCulture);
            }

            if (propertyType == typeof(TimeOnly) && value is TimeOnly timeOnly)
            {
                return timeOnly.ToString(TimeOnlyFormat, CultureInfo.CurrentCulture);
            }

            if (propertyType == typeof(DateTimeOffset) && value is DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset.ToString(DateTimeOffsetFormat, CultureInfo.CurrentCulture);
            }

            if (propertyType == typeof(DateTime) && value is DateTime dateTime)
            {
                return dateTime.ToString(DateTimeFormat, CultureInfo.CurrentCulture);
            }

            return value.ToString() ?? string.Empty;
        }

        private static void ResetDateEditor(PropertyInfo property, object obj, Type propertyType, DatePicker datePicker, TextBox? timeTextBox)
        {
            datePicker.SelectedDate = GetSelectedDate(property, obj, propertyType);
            if (timeTextBox != null)
            {
                timeTextBox.Text = GetDateTimeClockText(property, obj);
            }
        }

        private static bool CanSetNull(PropertyInfo property)
        {
            return !property.PropertyType.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) != null;
        }

        private static void CommitOnEnter(TextBox textBox, Action commitAction, KeyEventArgs eventArgs)
        {
            if (eventArgs.Key != Key.Enter)
            {
                return;
            }

            commitAction();
            textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            eventArgs.Handled = true;
        }
    }
}
