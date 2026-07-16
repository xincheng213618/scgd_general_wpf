using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.UI;
using log4net;
using Microsoft.Win32;
using Newtonsoft.Json;
using ProjectARVRPro.LegacyARVR;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ProjectARVRPro
{
    public partial class ObjectiveTestResultRecordWindow : Window
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ObjectiveTestResultRecordWindow));
        private CopilotDynamicContextSession? _copilotContextSession;
        private int _copilotPublishQueued;

        public ObservableCollection<ObjectiveTestResultRecord> Records { get; } = new();

        public ObjectiveTestResultRecordWindow()
        {
            InitializeComponent();
            RecordDataGrid.ItemsSource = Records;
            Records.CollectionChanged += Records_CollectionChanged;
            LoadRecords();
            RegisterCopilotContext();
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

        private void RegisterCopilotContext()
        {
            try
            {
                _copilotContextSession = ProjectARVRCopilotContextHub.Shared.Register(
                    CaptureCopilotSnapshotAsync,
                    typeof(ObjectiveTestResultRecordWindow).Assembly.GetName().Version?.ToString());
            }
            catch (Exception ex)
            {
                Log.Warn("Could not register the ARVRPro objective-result history Copilot context; the history window will continue to operate.", ex);
            }
        }

        private async Task<CopilotProjectResultContextSnapshot?> CaptureCopilotSnapshotAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Dispatcher.CheckAccess())
            {
                return await Dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CaptureCopilotSnapshot();
                });
            }

            return CaptureCopilotSnapshot();
        }

        private CopilotProjectResultContextSnapshot CaptureCopilotSnapshot()
        {
            return ProjectARVRCopilotSnapshotFactory.CreateForObjectiveResultRecords(
                "ARVRPro objective test result history",
                Records,
                SelectedRecord);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            _copilotContextSession?.Activate();
            PublishCopilotContext();
        }

        private void RecordDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _copilotContextSession?.Activate();
            QueueCopilotContextPublish();
        }

        private void Records_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            QueueCopilotContextPublish();
        }

        private void QueueCopilotContextPublish()
        {
            if (Interlocked.Exchange(ref _copilotPublishQueued, 1) != 0)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Interlocked.Exchange(ref _copilotPublishQueued, 0);
                PublishCopilotContext();
            }));
        }

        private void PublishCopilotContext()
        {
            if (_copilotContextSession?.IsCurrent != true || !IsActive)
                return;

            try
            {
                var item = CopilotBusinessContextBuilder.BuildProjectResultContextItem(CaptureCopilotSnapshot());
                CopilotBusinessContextCoordinator.Publish(CopilotBusinessContextBundle.FromItem(
                    ProjectARVRCopilotAgentExtension.SourceId,
                    item));
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not publish the active ARVRPro objective-result history context to Copilot: {ex.Message}");
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

        protected override void OnClosed(EventArgs e)
        {
            Records.CollectionChanged -= Records_CollectionChanged;
            var wasCurrent = _copilotContextSession?.IsCurrent == true;
            _copilotContextSession?.Dispose();
            _copilotContextSession = null;
            if (wasCurrent)
                CopilotLiveContextRegistry.Clear(ProjectARVRCopilotAgentExtension.SourceId);
            base.OnClosed(e);
        }
    }
}
