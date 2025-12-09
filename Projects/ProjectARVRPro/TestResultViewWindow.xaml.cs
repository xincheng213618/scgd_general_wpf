using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.Win32;

namespace ProjectARVRPro
{
    /// <summary>
    /// Converter to convert bool to "PASS" or "FAIL" string
    /// </summary>
    public class BoolToPassFailConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? "PASS" : "FAIL";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to convert bool to color (Green for PASS, Red for FAIL)
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Brushes.Green : Brushes.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Window to display ObjectiveTestItem list from ViewResultJson
    /// </summary>
    public partial class TestResultViewWindow : Window
    {
        public ObservableCollection<ObjectiveTestItem> TestItems { get; set; } = new ObservableCollection<ObjectiveTestItem>();

        public TestResultViewWindow(string viewResultJson)
        {
            InitializeComponent();
            ParseAndDisplayTestResult(viewResultJson);
            dataGrid.ItemsSource = TestItems;
        }

        private void ParseAndDisplayTestResult(string viewResultJson)
        {
            if (string.IsNullOrWhiteSpace(viewResultJson))
                return;

            try
            {
                // Try to parse the JSON and extract ObjectiveTestItem properties
                var obj = JsonConvert.DeserializeObject<object>(viewResultJson);
                if (obj != null)
                {
                    CollectTestItems(obj, TestItems);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to parse ViewResultJson: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CollectTestItems(object obj, ObservableCollection<ObjectiveTestItem> items)
        {
            if (obj == null) return;

            // Handle JObject from JSON deserialization
            if (obj is Newtonsoft.Json.Linq.JObject jObject)
            {
                foreach (var property in jObject.Properties())
                {
                    if (property.Value is Newtonsoft.Json.Linq.JObject childObj)
                    {
                        // Check if this looks like an ObjectiveTestItem
                        if (childObj.ContainsKey("Name") && childObj.ContainsKey("Value"))
                        {
                            try
                            {
                                var testItem = childObj.ToObject<ObjectiveTestItem>();
                                if (testItem != null)
                                {
                                    items.Add(testItem);
                                }
                            }
                            catch
                            {
                                // Not an ObjectiveTestItem, try to recurse
                                CollectTestItems(childObj, items);
                            }
                        }
                        else
                        {
                            // Recurse into child object
                            CollectTestItems(childObj, items);
                        }
                    }
                    else if (property.Value is Newtonsoft.Json.Linq.JArray jArray)
                    {
                        // Handle arrays
                        foreach (var arrayItem in jArray)
                        {
                            if (arrayItem is Newtonsoft.Json.Linq.JObject arrayObj)
                            {
                                CollectTestItems(arrayObj, items);
                            }
                        }
                    }
                }
            }
            else
            {
                // Handle regular .NET objects
                foreach (var property in obj.GetType().GetProperties())
                {
                    if (property.PropertyType == typeof(ObjectiveTestItem))
                    {
                        var testItem = (ObjectiveTestItem)property.GetValue(obj);
                        if (testItem != null)
                        {
                            items.Add(testItem);
                        }
                    }
                    else if (!property.PropertyType.IsValueType && property.PropertyType != typeof(string))
                    {
                        try
                        {
                            var childObj = property.GetValue(obj);
                            if (childObj != null)
                            {
                                CollectTestItems(childObj, items);
                            }
                        }
                        catch
                        {
                            // Ignore errors
                        }
                    }
                }
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (TestItems.Count == 0)
            {
                MessageBox.Show("没有可导出的数据", "导出 CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"TestResults_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                using var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8);
                writer.WriteLine("Name,TestValue,Value,LowLimit,UpLimit,Unit,Result");
                foreach (var item in TestItems)
                {
                    writer.WriteLine(string.Join(",", new[]
                    {
                        EscapeCsv(item.Name),
                        EscapeCsv(item.TestValue),
                        item.Value.ToString("F4", CultureInfo.InvariantCulture),
                        item.LowLimit.ToString("F4", CultureInfo.InvariantCulture),
                        item.UpLimit.ToString("F4", CultureInfo.InvariantCulture),
                        EscapeCsv(item.Unit),
                        item.TestResult ? "PASS" : "FAIL"
                    }));
                }
                MessageBox.Show("CSV 导出完成", "导出 CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "导出 CSV", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (TestItems.Count == 0)
            {
                MessageBox.Show("没有可导出的数据", "导出 PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"TestResults_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                using var writer = new PdfWriter(dialog.FileName);
                using var pdf = new PdfDocument(writer);
                using var document = new Document(pdf);

                document.Add(new Paragraph("ObjectiveTestItem List").SetBold().SetFontSize(14));

                var table = new Table(new float[] { 2, 2, 1, 1, 1, 1, 1 }).UseAllAvailableWidth();
                string[] headers = { "Name", "TestValue", "Value", "LowLimit", "UpLimit", "Unit", "Result" };
                foreach (var header in headers)
                {
                    table.AddHeaderCell(new Cell().Add(new Paragraph(header).SetBold()).SetTextAlignment(TextAlignment.CENTER));
                }

                foreach (var item in TestItems)
                {
                    table.AddCell(new Paragraph(item.Name ?? string.Empty));
                    table.AddCell(new Paragraph(item.TestValue ?? string.Empty));
                    table.AddCell(new Paragraph(item.Value.ToString("F4", CultureInfo.InvariantCulture)));
                    table.AddCell(new Paragraph(item.LowLimit.ToString("F4", CultureInfo.InvariantCulture)));
                    table.AddCell(new Paragraph(item.UpLimit.ToString("F4", CultureInfo.InvariantCulture)));
                    table.AddCell(new Paragraph(item.Unit ?? string.Empty));
                    table.AddCell(new Paragraph(item.TestResult ? "PASS" : "FAIL"));
                }

                document.Add(table);
                MessageBox.Show("PDF 导出完成", "导出 PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "导出 PDF", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static readonly char[] CsvSpecialCharacters = { '"', ',', '\n', '\r' };
        private static string EscapeCsv(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            bool requiresQuotes = input.IndexOfAny(CsvSpecialCharacters) >= 0;
            if (requiresQuotes)
            {
                return $"\"{input.Replace("\"", "\"\"")}\"";
            }

            return input;
        }
    }
}
