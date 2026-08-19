#pragma warning disable CA1822
using ColorVision.UI;
using ColorVision.Solution.Editor.AvalonEditor;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

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

        public string ViewResultJson { get; set; } = string.Empty;
        public TestResultViewWindow(string viewResultJson)
        {
            ViewResultJson = viewResultJson;
            InitializeComponent();
            ParseAndDisplayTestResult(viewResultJson);
            dataGrid.ItemsSource = TestItems;
        }
        private void OpenJson_Click(object sender, RoutedEventArgs e)
        {
            var control = new AvalonEditControll();
            control.SetJsonText(ViewResultJson);
            Window window = new Window
            {
                Title = "ViewResultJson",
                Content = control,
                Width = 800,
                Height = 600
            };
            window.Show();
        }
        private void ParseAndDisplayTestResult(string viewResultJson)
        {
            if (string.IsNullOrWhiteSpace(viewResultJson))
                return;
            try
            {
                foreach (var item in ObjectiveTestItemCollector.CollectFromJson(viewResultJson))
                    TestItems.Add(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to parse ViewResultJson: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (TestItems.Count == 0)
            {
                MessageBox.Show("没有可导出的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"TestResult_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExportToCsv(saveFileDialog.FileName);
                    MessageBox.Show($"数据已成功导出到:\n{saveFileDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出CSV失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportToCsv(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,TestValue,Value,LowLimit,UpLimit,Unit,TestResult");

            foreach (var item in TestItems)
            {
                string testResult = item.TestResult ? "PASS" : "FAIL";
                sb.AppendLine($"{EscapeCsvField(item.Name)},{EscapeCsvField(item.TestValue)},{item.Value:F4},{item.LowLimit:F4},{item.UpLimit:F4},{EscapeCsvField(item.Unit)},{testResult}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsvField(string? field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // If field contains special characters, wrap in quotes and escape existing quotes
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }

    }
}
