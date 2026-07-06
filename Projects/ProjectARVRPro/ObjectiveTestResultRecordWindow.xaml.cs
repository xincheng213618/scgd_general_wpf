using ColorVision.Solution.Editor.AvalonEditor;
using Microsoft.Win32;
using Newtonsoft.Json;
using ProjectARVRPro.LegacyARVR;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ProjectARVRPro
{
    public partial class ObjectiveTestResultRecordWindow : Window
    {
        public ObservableCollection<ObjectiveTestResultRecord> Records { get; } = new();

        public ObjectiveTestResultRecordWindow()
        {
            InitializeComponent();
            RecordDataGrid.ItemsSource = Records;
            LoadRecords();
        }

        private ObjectiveTestResultRecord? SelectedRecord => RecordDataGrid.SelectedItem as ObjectiveTestResultRecord;

        private void Query_Click(object sender, RoutedEventArgs e)
        {
            LoadRecords();
        }

        private void LoadRecords()
        {
            int count = 100;
            if (int.TryParse(CountTextBox.Text, out int parsedCount) && parsedCount > 0)
            {
                count = parsedCount;
            }

            Records.Clear();
            foreach (var record in ViewResultManager.GetInstance().QueryObjectiveTestResultRecords(SnTextBox.Text, count))
            {
                Records.Add(record);
            }
        }

        private void ViewItems_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedRecord(out var record)) return;
            var window = new TestResultViewWindow(record.ObjectiveTestResultJson)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
        }

        private void ViewJson_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedRecord(out var record)) return;

            var control = new AvalonEditControll();
            control.SetJsonText(record.ObjectiveTestResultJson);
            var window = new Window
            {
                Title = $"ObjectiveTestResult Json - {record.SN}",
                Owner = this,
                Content = control,
                Width = 900,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedRecord(out var record)) return;

            var result = JsonConvert.DeserializeObject<ObjectiveTestResult>(record.ObjectiveTestResultJson);
            if (result == null)
            {
                MessageBox.Show(this, "ObjectiveTestResult 为空，无法导出", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new OpenFolderDialog
            {
                Title = "导出 ObjectiveTestResult",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };
            if (dialog.ShowDialog(this) != true) return;

            string sn = SanitizeFileName(string.IsNullOrWhiteSpace(record.SN) ? "SN" : record.SN);
            string path = Path.Combine(dialog.FolderName, $"TestResults_{sn}.csv");
            try
            {
                if (ViewResultManager.GetInstance().Config.UseLegacyARVROutput)
                {
                    var legacyResult = LegacyARVRConverter.ToLegacy(result);
                    LegacyARVRCsvExporter.ExportToCsv(new List<LegacyARVRObjectiveTestResult> { legacyResult }, path);
                }
                else
                {
                    ObjectiveTestResultCsvExporter.ExportToCsv(result, path);
                }

                MessageBox.Show(this, "导出完成：" + path, "ColorVision");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：" + ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryGetSelectedRecord(out ObjectiveTestResultRecord record)
        {
            var selectedRecord = SelectedRecord;
            if (selectedRecord != null)
            {
                record = selectedRecord;
                return true;
            }

            record = null!;
            MessageBox.Show(this, "请先选择一条记录", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }
    }
}
