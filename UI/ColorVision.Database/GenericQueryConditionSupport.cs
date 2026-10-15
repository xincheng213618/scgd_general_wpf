#pragma warning disable CA1863
using ColorVision.Common.Utilities;
using ColorVision.Database.Properties;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Database
{
    internal sealed class QueryOperatorOption
    {
        public QueryOperatorOption(QueryOperator value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public QueryOperator Value { get; }
        public string DisplayName { get; }
    }

    internal sealed class QueryValueOption
    {
        public QueryValueOption(object value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public object Value { get; }
        public string DisplayName { get; }
    }

    internal static class GenericQueryConditionSupport
    {
        private static readonly Type[] NumericTypes =
        [
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal)
        ];

        public static IReadOnlyList<KeyValuePair<string, PropertyInfo>> GetQueryableProperties(Type entityType)
        {
            return entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(IsQueryableProperty)
                .Select(property => new KeyValuePair<string, PropertyInfo>(GetDisplayName(property), property))
                .OrderBy(item => GetPropertyPriority(item.Value.Name))
                .ThenBy(item => item.Key, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static string GetDisplayName(PropertyInfo property)
        {
            var display = property.GetCustomAttribute<DisplayAttribute>();
            if (display != null)
            {
                try
                {
                    var name = display.GetName();
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
                catch (InvalidOperationException)
                {
                    // Fall through to the next available metadata source.
                }
            }

            var displayName = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            return property.Name switch
            {
                "Id" => Resources.DB_FieldId,
                "Name" => Resources.DB_FieldName,
                "Code" => Resources.DB_FieldCode,
                "Model" => Resources.DB_FieldModel,
                "Result" => Resources.DB_FieldResult,
                "FlowStatus" => Resources.DB_FieldFlowStatus,
                "CreateTime" or "CreateDate" => Resources.DB_FieldCreateTime,
                "UpdateTime" => Resources.DB_FieldUpdateTime,
                "FileName" => Resources.DB_FieldFileName,
                "Msg" => Resources.DB_FieldMessage,
                "ZIndex" => Resources.DB_FieldZIndex,
                "BatchId" => Resources.DB_FieldBatchId,
                "TestType" => Resources.DB_FieldTestType,
                "RunTime" => Resources.DB_FieldRunTime,
                _ => property.Name
            };
        }

        public static string GetColumnName(PropertyInfo property)
        {
            var sugarColumn = property.GetCustomAttribute<SugarColumn>();
            return string.IsNullOrWhiteSpace(sugarColumn?.ColumnName) ? property.Name : sugarColumn.ColumnName;
        }

        public static FrameworkElement CreateConditionRow(QueryCondition condition,
            IEnumerable<KeyValuePair<string, PropertyInfo>> properties, RoutedEventHandler removeHandler)
        {
            var rowBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            rowBorder.SetResourceReference(Border.BackgroundProperty, "GlobalBorderBrush");
            rowBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            rowGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rowGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var operatorComboBox = new ComboBox
            {
                DisplayMemberPath = nameof(QueryOperatorOption.DisplayName),
                SelectedValuePath = nameof(QueryOperatorOption.Value),
                MinHeight = 30,
                Margin = new Thickness(0, 0, 8, 0)
            };
            operatorComboBox.SelectionChanged += (_, _) =>
            {
                if (operatorComboBox.SelectedValue is QueryOperator selectedOperator)
                    condition.Operator = selectedOperator;
            };
            Grid.SetColumn(operatorComboBox, 1);
            rowGrid.Children.Add(operatorComboBox);

            var valueHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, IsTabStop = false };
            Grid.SetColumn(valueHost, 2);
            rowGrid.Children.Add(valueHost);

            var removeButton = new Button
            {
                Content = "×",
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                Margin = new Thickness(8, 0, 0, 0),
                ToolTip = Resources.DB_RemoveCondition,
                Tag = condition
            };
            removeButton.Click += removeHandler;
            Grid.SetColumn(removeButton, 3);
            rowGrid.Children.Add(removeButton);

            var errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(229, 57, 53)),
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            AutomationProperties.SetLiveSetting(errorText, AutomationLiveSetting.Assertive);
            condition.ErrorText = errorText;
            Grid.SetRow(errorText, 1);
            Grid.SetColumnSpan(errorText, 4);
            rowGrid.Children.Add(errorText);

            void RefreshEditors()
            {
                string displayName = condition.Property == null ? Resources.DB_SelectFilterField : GetDisplayName(condition.Property);
                var operators = condition.Property == null ? [] : GetOperatorOptions(condition.Property.PropertyType);
                // Capture the operator before replacing ItemsSource, which raises SelectionChanged.
                QueryOperator selectedOperator = condition.HasSavedState && operators.Any(option => option.Value == condition.Operator)
                    ? condition.Operator : operators.FirstOrDefault()?.Value ?? QueryOperator.Equal;
                operatorComboBox.ItemsSource = operators;
                operatorComboBox.SelectedValue = selectedOperator;
                operatorComboBox.IsEnabled = condition.Property != null;
                AutomationProperties.SetName(operatorComboBox, string.Format(Resources.DB_FilterOperatorAutomationName, displayName));
                AutomationProperties.SetName(removeButton, string.Format(Resources.DB_RemoveNamedCondition, displayName));
                condition.ValueEditor = condition.Property == null ? new TextBox { MinHeight = 30, IsEnabled = false } : CreateValueEditor(condition, displayName);
                valueHost.Content = condition.ValueEditor;
            }

            RefreshEditors();
            var fieldEditor = CreateFieldEditor(condition, properties, () =>
            {
                ClearError(condition);
                RefreshEditors();
            });
            condition.FieldEditor = fieldEditor;
            var fieldHost = new Grid();
            fieldHost.Children.Add(fieldEditor);
            var fieldHint = new TextBlock
            {
                Text = Resources.DB_SelectFilterField,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 30, 0),
                Opacity = 0.6,
                IsHitTestVisible = false,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Visibility = string.IsNullOrEmpty(fieldEditor.Text) ? Visibility.Visible : Visibility.Collapsed
            };
            fieldHint.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            fieldEditor.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) =>
                fieldHint.Visibility = string.IsNullOrEmpty(fieldEditor.Text) ? Visibility.Visible : Visibility.Collapsed));
            fieldHost.Children.Add(fieldHint);
            rowGrid.Children.Insert(0, fieldHost);

            rowBorder.Child = rowGrid;
            condition.UiRow = rowBorder;
            return rowBorder;
        }

        private static ComboBox CreateFieldEditor(QueryCondition condition,
            IEnumerable<KeyValuePair<string, PropertyInfo>> properties, Action fieldChanged)
        {
            // Each row owns its view: searching one row must not filter other rows.
            var options = properties.Select(item => new KeyValuePair<string, PropertyInfo>(
                item.Key == item.Value.Name ? item.Key : $"{item.Key} ({item.Value.Name})", item.Value)).ToList();
            var view = new ListCollectionView(options);
            var comboBox = new ComboBox
            {
                ItemsSource = view,
                DisplayMemberPath = "Key",
                SelectedValuePath = "Value",
                SelectedValue = condition.Property,
                IsSynchronizedWithCurrentItem = false,
                IsEditable = true,
                IsTextSearchEnabled = false,
                StaysOpenOnEdit = true,
                MinHeight = 30,
                MaxDropDownHeight = 300,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = Resources.DB_SearchFilterField
            };
            AutomationProperties.SetName(comboBox, Resources.DB_SelectFilterField);
            AutomationProperties.SetHelpText(comboBox, Resources.DB_SearchFilterField);
            bool updating = false;

            void ChangeField(PropertyInfo? property)
            {
                if (condition.Property == property)
                    return;
                condition.Property = property;
                condition.InputText = null;
                condition.Value = null;
                condition.HasSavedState = false;
                fieldChanged();
            }

            comboBox.SelectionChanged += (_, _) =>
            {
                if (updating || comboBox.SelectedValue is not PropertyInfo property)
                    return;
                condition.FieldInputText = string.Empty;
                ChangeField(property);
                comboBox.ToolTip = GetColumnName(property);
            };
            comboBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, e) =>
            {
                if (updating || e.OriginalSource is not TextBox textBox)
                    return;
                string search = textBox.Text;
                if (comboBox.SelectedItem is KeyValuePair<string, PropertyInfo> selected && search == selected.Key)
                    return;

                updating = true;
                try
                {
                    int caret = textBox.CaretIndex;
                    comboBox.SelectedIndex = -1;
                    ChangeField(null);
                    condition.FieldInputText = search;
                    view.Filter = item => item is KeyValuePair<string, PropertyInfo> option
                        && MatchesField(option, search);
                    comboBox.Text = search;
                    textBox.CaretIndex = Math.Min(caret, search.Length);
                    if (comboBox.IsLoaded && comboBox.IsKeyboardFocusWithin)
                        comboBox.IsDropDownOpen = true;
                }
                finally { updating = false; }
            }));
            comboBox.DropDownOpened += (_, _) =>
            {
                if (condition.Property != null)
                    view.Filter = null;
            };
            comboBox.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    if (condition.Property == null && view.Count == 1)
                        comboBox.SelectedIndex = 0;
                    if (condition.Property != null)
                    {
                        comboBox.IsDropDownOpen = false;
                        condition.ValueEditor?.Focus();
                    }
                    // Field selection must not also trigger the window's default Query button.
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape && comboBox.IsDropDownOpen)
                {
                    comboBox.IsDropDownOpen = false;
                    e.Handled = true;
                }
            };
            return comboBox;
        }

        internal static bool MatchesField(KeyValuePair<string, PropertyInfo> field, string search)
        {
            search = search.Trim();
            return field.Key.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || field.Value.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || GetColumnName(field.Value).Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        public static ISugarQueryable<T> ApplyConditions<T>(ISugarQueryable<T> query, IEnumerable<QueryCondition> conditions)
        {
            var index = 0;
            foreach (var condition in conditions)
            {
                ClearError(condition);
                if (condition.Property == null)
                {
                    if (string.IsNullOrWhiteSpace(condition.FieldInputText))
                        continue;
                    SetError(condition, Resources.DB_SelectFilterField);
                    throw new FormatException(Resources.DB_SelectFilterField);
                }
                if (!HasConditionValue(condition))
                    continue;

                if (!TryGetConditionValue(condition, out var value, out var error))
                {
                    SetError(condition, error);
                    throw new FormatException(error);
                }

                var propertyType = Nullable.GetUnderlyingType(condition.Property.PropertyType) ?? condition.Property.PropertyType;
                if (propertyType.IsEnum)
                    value = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                else if (condition.Operator == QueryOperator.Like)
                    value = $"%{value}%";

                var parameterName = $"queryValue{index++}";
                var parameters = new Dictionary<string, object> { [parameterName] = value! };
                query = query.Where($"{GetColumnName(condition.Property)} {condition.Operator.ToDescription()} @{parameterName}", parameters);
            }

            return query;
        }

        internal static bool HasConditionValue(QueryCondition condition)
        {
            if (condition.Property == null)
                return false;
            var propertyType = Nullable.GetUnderlyingType(condition.Property.PropertyType) ?? condition.Property.PropertyType;
            return propertyType.IsEnum || propertyType == typeof(bool) || propertyType == typeof(DateTime)
                ? condition.Value != null
                : !string.IsNullOrWhiteSpace(condition.InputText);
        }

        internal static bool TryGetConditionValue(QueryCondition condition, out object? value, out string error)
        {
            if (condition.Property == null)
            {
                value = null;
                error = Resources.DB_SelectFilterField;
                return false;
            }
            var propertyType = Nullable.GetUnderlyingType(condition.Property.PropertyType) ?? condition.Property.PropertyType;
            var displayName = GetDisplayName(condition.Property);

            if (propertyType == typeof(string))
            {
                value = condition.InputText?.Trim();
                if (string.IsNullOrWhiteSpace((string?)value))
                {
                    error = string.Format(Resources.DB_FilterValueRequired, displayName);
                    return false;
                }

                error = string.Empty;
                return true;
            }

            if (propertyType.IsEnum || propertyType == typeof(bool) || propertyType == typeof(DateTime))
            {
                value = condition.Value;
                if (value == null)
                {
                    error = string.Format(Resources.DB_FilterValueRequired, displayName);
                    return false;
                }

                error = string.Empty;
                return true;
            }

            var input = condition.InputText?.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                value = null;
                error = string.Format(Resources.DB_FilterValueRequired, displayName);
                return false;
            }

            try
            {
                var converter = TypeDescriptor.GetConverter(propertyType);
                value = converter.ConvertFromString(null, CultureInfo.CurrentCulture, input);
                error = string.Empty;
                return value != null;
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or NotSupportedException or OverflowException)
            {
                value = null;
                error = string.Format(Resources.DB_FilterValueInvalid, displayName, input);
                return false;
            }
        }

        private static Control CreateValueEditor(QueryCondition condition, string displayName)
        {
            var propertyType = Nullable.GetUnderlyingType(condition.Property!.PropertyType) ?? condition.Property.PropertyType;
            if (propertyType.IsEnum)
            {
                var values = Enum.GetValues(propertyType)
                    .Cast<Enum>()
                    .Select(value => new QueryValueOption(value, value.ToDescription()))
                    .ToList();
                var comboBox = CreateValueComboBox(values, displayName);
                comboBox.SelectionChanged += (_, _) => condition.Value = comboBox.SelectedValue;
                comboBox.SelectedValue = condition.Value;
                return comboBox;
            }

            if (propertyType == typeof(bool))
            {
                var values = new List<QueryValueOption>
                {
                    new(true, Resources.DB_BooleanTrue),
                    new(false, Resources.DB_BooleanFalse)
                };
                var comboBox = CreateValueComboBox(values, displayName);
                comboBox.SelectionChanged += (_, _) => condition.Value = comboBox.SelectedValue;
                comboBox.SelectedValue = condition.Value;
                return comboBox;
            }

            if (propertyType == typeof(DateTime))
            {
                var datePicker = new DatePicker
                {
                    MinHeight = 30,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                AutomationProperties.SetName(datePicker, string.Format(Resources.DB_FilterValueAutomationName, displayName));
                datePicker.SelectedDateChanged += (_, _) => condition.Value = datePicker.SelectedDate;
                datePicker.SelectedDate = condition.Value as DateTime?;
                return datePicker;
            }

            var textBox = new TextBox
            {
                MinHeight = 30,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = string.Format(Resources.DB_FilterValueAutomationName, displayName)
            };
            AutomationProperties.SetName(textBox, string.Format(Resources.DB_FilterValueAutomationName, displayName));
            textBox.TextChanged += (_, _) => condition.InputText = textBox.Text;
            textBox.Text = condition.InputText ?? string.Empty;
            return textBox;
        }

        private static ComboBox CreateValueComboBox(IReadOnlyList<QueryValueOption> values, string displayName)
        {
            var comboBox = new ComboBox
            {
                ItemsSource = values,
                DisplayMemberPath = nameof(QueryValueOption.DisplayName),
                SelectedValuePath = nameof(QueryValueOption.Value),
                SelectedIndex = -1,
                MinHeight = 30
            };
            AutomationProperties.SetName(comboBox, string.Format(Resources.DB_FilterValueAutomationName, displayName));
            return comboBox;
        }

        private static IReadOnlyList<QueryOperatorOption> GetOperatorOptions(Type type)
        {
            var propertyType = Nullable.GetUnderlyingType(type) ?? type;
            if (propertyType == typeof(string))
            {
                return
                [
                    new(QueryOperator.Like, Resources.DB_OperatorContains),
                    new(QueryOperator.Equal, Resources.DB_OperatorEqual),
                    new(QueryOperator.NotEqual, Resources.DB_OperatorNotEqual)
                ];
            }

            if (propertyType.IsEnum || propertyType == typeof(bool))
            {
                return
                [
                    new(QueryOperator.Equal, Resources.DB_OperatorEqual),
                    new(QueryOperator.NotEqual, Resources.DB_OperatorNotEqual)
                ];
            }

            return
            [
                new(QueryOperator.Equal, Resources.DB_OperatorEqual),
                new(QueryOperator.NotEqual, Resources.DB_OperatorNotEqual),
                new(QueryOperator.Greater, Resources.DB_OperatorGreater),
                new(QueryOperator.GreaterOrEqual, Resources.DB_OperatorGreaterOrEqual),
                new(QueryOperator.Less, Resources.DB_OperatorLess),
                new(QueryOperator.LessOrEqual, Resources.DB_OperatorLessOrEqual)
            ];
        }

        private static bool IsQueryableProperty(PropertyInfo property)
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
                return false;

            var sugarColumn = property.GetCustomAttribute<SugarColumn>();
            if (sugarColumn?.IsIgnore == true)
                return false;

            if (property.GetCustomAttribute<BrowsableAttribute>()?.Browsable == false)
                return false;

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            return propertyType == typeof(string)
                || propertyType == typeof(bool)
                || propertyType == typeof(DateTime)
                || propertyType == typeof(Guid)
                || propertyType.IsEnum
                || NumericTypes.Contains(propertyType);
        }

        private static int GetPropertyPriority(string propertyName)
        {
            return propertyName switch
            {
                "SN" => 0,
                "Code" => 1,
                "Name" => 2,
                "Model" => 3,
                "Result" => 4,
                "FlowStatus" => 5,
                "CreateTime" or "CreateDate" or "SendTime" => 6,
                "UpdateTime" => 7,
                "Id" => 8,
                _ => 100
            };
        }

        private static void SetError(QueryCondition condition, string error)
        {
            if (condition.ErrorText != null)
            {
                condition.ErrorText.Text = error;
                condition.ErrorText.Visibility = Visibility.Visible;
            }

            (condition.Property == null ? condition.FieldEditor : condition.ValueEditor)?.Focus();
        }

        private static void ClearError(QueryCondition condition)
        {
            if (condition.ErrorText != null)
            {
                condition.ErrorText.Text = string.Empty;
                condition.ErrorText.Visibility = Visibility.Collapsed;
            }
        }
    }
}
