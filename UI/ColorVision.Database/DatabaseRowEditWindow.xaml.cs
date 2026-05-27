using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Database.Properties;

namespace ColorVision.Database
{
    public partial class DatabaseRowEditWindow : Window
    {
        private readonly List<DatabaseColumnInfo> _columns;
        private readonly Dictionary<string, TextBox> _editors = new();

        public DatabaseRowEditWindow(IReadOnlyList<DatabaseColumnInfo> columns, string title)
        {
            _columns = columns
                .Where(column => !column.IsIdentity && !column.IsReadOnly)
                .OrderBy(column => column.Ordinal)
                .ToList();

            InitializeComponent();
            Title = title;
            TitleText.Text = title;
            BuildEditors();
        }

        public Dictionary<string, object?> Values { get; } = new();

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.ApplyCaption();
        }

        private void BuildEditors()
        {
            EditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            EditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rowIndex = 0;
            foreach (var column in _columns)
            {
                EditorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new TextBlock
                {
                    Text = column.ColumnName,
                    ToolTip = BuildColumnToolTip(column),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 8)
                };
                Grid.SetRow(label, rowIndex);
                Grid.SetColumn(label, 0);
                EditorGrid.Children.Add(label);

                var editor = new TextBox
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    MinWidth = 240,
                    ToolTip = column.StoreType
                };
                if (column.IsPrimaryKey)
                    editor.ToolTip = string.IsNullOrWhiteSpace(column.StoreType) ? Properties.Resources.DB_PrimaryKey : $"{Properties.Resources.DB_PrimaryKey}, {column.StoreType}";

                Grid.SetRow(editor, rowIndex);
                Grid.SetColumn(editor, 1);
                EditorGrid.Children.Add(editor);
                _editors[column.ColumnName] = editor;

                rowIndex++;
            }

            if (_columns.Count == 0)
            {
                EditorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var text = new TextBlock
                {
                    Text = Properties.Resources.DB_NoInsertableColumn,
                    Opacity = 0.72,
                    Margin = new Thickness(0, 4, 0, 4)
                };
                Grid.SetRow(text, 0);
                Grid.SetColumnSpan(text, 2);
                EditorGrid.Children.Add(text);
            }
        }

        private static string BuildColumnToolTip(DatabaseColumnInfo column)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(column.StoreType)) parts.Add(column.StoreType);
            if (column.IsPrimaryKey) parts.Add("Primary Key");
            if (!column.IsNullable) parts.Add("Not Null");
            if (!string.IsNullOrWhiteSpace(column.Comment)) parts.Add(column.Comment);
            return string.Join(Environment.NewLine, parts);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Values.Clear();
                foreach (var column in _columns)
                {
                    var text = _editors[column.ColumnName].Text;
                    Values[column.ColumnName] = ConvertText(column, text);
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                ErrorText.Text = ex.Message;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static object? ConvertText(DatabaseColumnInfo column, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var storeType = column.StoreType.ToUpperInvariant();
            try
            {
                if (storeType.Contains("BIGINT"))
                    return long.Parse(text, CultureInfo.InvariantCulture);

                if (storeType.Contains("INT") || storeType.Contains("TINYINT") || storeType.Contains("SMALLINT") || storeType.Contains("MEDIUMINT"))
                    return int.Parse(text, CultureInfo.InvariantCulture);

                if (storeType.Contains("DECIMAL") || storeType.Contains("NUMERIC"))
                    return decimal.Parse(text, CultureInfo.InvariantCulture);

                if (storeType.Contains("DOUBLE") || storeType.Contains("FLOAT") || storeType.Contains("REAL"))
                    return double.Parse(text, CultureInfo.InvariantCulture);

                if (storeType.Contains("BOOL") || storeType == "BIT")
                    return bool.Parse(text);

                if (storeType.Contains("DATE") || storeType.Contains("TIME"))
                    return DateTime.Parse(text, CultureInfo.CurrentCulture);
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
                throw new FormatException($"{column.ColumnName} 的值无法转换为 {column.StoreType}。", ex);
            }

            return text;
        }
    }
}