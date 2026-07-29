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

        public static FrameworkElement CreateConditionRow(QueryCondition condition, RoutedEventHandler removeHandler)
        {
            var displayName = GetDisplayName(condition.Property);
            var columnName = GetColumnName(condition.Property);

            var rowBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };
            rowBorder.SetResourceReference(Border.BackgroundProperty, "GlobalBorderBrush");
            rowBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            rowGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rowGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var fieldLabel = new TextBlock
            {
                Text = displayName,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 12, 0),
                ToolTip = string.Equals(displayName, columnName, StringComparison.Ordinal) ? null : columnName
            };
            fieldLabel.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            Grid.SetColumn(fieldLabel, 0);
            rowGrid.Children.Add(fieldLabel);

            var operators = GetOperatorOptions(condition.Property.PropertyType);
            if (!condition.HasSavedState || !operators.Any(option => option.Value == condition.Operator))
                condition.Operator = operators[0].Value;
            var operatorComboBox = new ComboBox
            {
                ItemsSource = operators,
                DisplayMemberPath = nameof(QueryOperatorOption.DisplayName),
                SelectedValuePath = nameof(QueryOperatorOption.Value),
                SelectedValue = condition.Operator,
                MinHeight = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            AutomationProperties.SetName(operatorComboBox, string.Format(Resources.DB_FilterOperatorAutomationName, displayName));
            operatorComboBox.SelectionChanged += (_, _) =>
            {
                if (operatorComboBox.SelectedValue is QueryOperator selectedOperator)
                    condition.Operator = selectedOperator;
            };
            Grid.SetColumn(operatorComboBox, 1);
            rowGrid.Children.Add(operatorComboBox);

            var valueEditor = CreateValueEditor(condition, displayName);
            condition.ValueEditor = valueEditor;
            Grid.SetColumn(valueEditor, 2);
            rowGrid.Children.Add(valueEditor);

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
            AutomationProperties.SetName(removeButton, string.Format(Resources.DB_RemoveNamedCondition, displayName));
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
            Grid.SetColumn(errorText, 2);
            Grid.SetColumnSpan(errorText, 2);
            rowGrid.Children.Add(errorText);

            rowBorder.Child = rowGrid;
            condition.UiRow = rowBorder;
            return rowBorder;
        }

        public static ISugarQueryable<T> ApplyConditions<T>(ISugarQueryable<T> query, IEnumerable<QueryCondition> conditions)
        {
            var index = 0;
            foreach (var condition in conditions)
            {
                ClearError(condition);
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
            var propertyType = Nullable.GetUnderlyingType(condition.Property.PropertyType) ?? condition.Property.PropertyType;
            return propertyType.IsEnum || propertyType == typeof(bool) || propertyType == typeof(DateTime)
                ? condition.Value != null
                : !string.IsNullOrWhiteSpace(condition.InputText);
        }

        internal static bool TryGetConditionValue(QueryCondition condition, out object? value, out string error)
        {
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
            var propertyType = Nullable.GetUnderlyingType(condition.Property.PropertyType) ?? condition.Property.PropertyType;
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

            condition.ValueEditor?.Focus();
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
