#pragma warning disable CA1001,CA1863
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.UI;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine
{
    /// <summary>
    /// MeasureBatchPage.xaml 的交互逻辑
    /// </summary>
    public partial class MeasureBatchPage : Page
    {
        public Frame Frame { get; set; }
        public MeasureBatchModel MeasureBatchModel { get; set; }
        private CopilotDynamicContextSession? _copilotContextSession;
        private Window? _copilotHostWindow;
        private string _lastSelectedResultKind = string.Empty;

        public MeasureBatchPage(Frame frame, MeasureBatchModel measureBatchModel)
        {
            Frame = frame;
            MeasureBatchModel = measureBatchModel;
            InitializeComponent();
        }

        public ObservableCollection<ViewResultImage> ViewResultImages { get; set; } = new ObservableCollection<ViewResultImage>();
        public ObservableCollection<ViewResultAlg> ViewResultAlgs { get; set; } = new ObservableCollection<ViewResultAlg>();


        private void Page_Initialized(object sender, EventArgs e)
        {
            
            Title = string.Format(Properties.Resources.Flow_MeasureBatch_BatchResultTitle, MeasureBatchModel.Code);

            listView1.ItemsSource = ViewResultImages;
            listView2.ItemsSource = ViewResultAlgs;

        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewResultImages.Clear();
            foreach (var item in MeasureImgResultDao.Instance.GetAllByBatchId(MeasureBatchModel.Id))
            {
                ViewResultImages.Add(new ViewResultImage(item));
            }

            ViewResultAlgs.Clear();
            foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchId(MeasureBatchModel.Id))
            {
                ViewResultAlgs.Add(new ViewResultAlg(item));
            }
            EnsureCopilotContextRegistered();
            PublishCopilotContext();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            ReleaseCopilotContext();
        }

        private void listView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                ViewResultImages[listView.SelectedIndex].Open();
            }
        }

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(sender, listView1) && listView1.SelectedItem != null)
                _lastSelectedResultKind = "image";
            else if (ReferenceEquals(sender, listView2) && listView2.SelectedItem != null)
                _lastSelectedResultKind = "algorithm";
            _copilotContextSession?.Activate();
            PublishCopilotContext();
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        AlgorithmView? AlgorithmView;
        private void listView2_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                if (AlgorithmView == null)
                {
                    AlgorithmView = new AlgorithmView();
                    Window window = new Window() { Content = AlgorithmView ,Owner =Application.Current.GetActiveWindow() };
                    window.Closed += (s, args) => AlgorithmView = null; // 订阅窗口关闭事件
                    window.Show();
                }

                AlgorithmView.ViewResults.Clear();
                AlgorithmView.ViewResults.Add(ViewResultAlgs[listView.SelectedIndex]);
                AlgorithmView.RefreshResultListView();
            }
        }

        private void EnsureCopilotContextRegistered()
        {
            if (_copilotContextSession != null)
                return;

            try
            {
                _copilotContextSession = CopilotMeasurementResultContextHub.Shared.Register(
                    CaptureCopilotMeasurementResultSnapshotAsync,
                    typeof(MeasureBatchPage).Assembly.GetName().Version?.ToString());
                _copilotHostWindow = Window.GetWindow(this);
                if (_copilotHostWindow != null)
                {
                    _copilotHostWindow.Activated += CopilotHostWindow_Activated;
                    _copilotHostWindow.Closed += CopilotHostWindow_Closed;
                }
                _copilotContextSession.Activate();
            }
            catch (Exception ex)
            {
                log4net.LogManager.GetLogger(typeof(MeasureBatchPage)).Warn("注册检测结果 Copilot 上下文失败，结果页面将继续运行", ex);
            }
        }

        private void CopilotHostWindow_Activated(object? sender, EventArgs e)
        {
            _copilotContextSession?.Activate();
            PublishCopilotContext();
        }

        private void CopilotHostWindow_Closed(object? sender, EventArgs e)
        {
            ReleaseCopilotContext();
        }

        private void ReleaseCopilotContext()
        {
            if (_copilotHostWindow != null)
            {
                _copilotHostWindow.Activated -= CopilotHostWindow_Activated;
                _copilotHostWindow.Closed -= CopilotHostWindow_Closed;
                _copilotHostWindow = null;
            }

            var wasCurrent = _copilotContextSession?.IsCurrent == true;
            _copilotContextSession?.Dispose();
            _copilotContextSession = null;
            if (wasCurrent)
                CopilotLiveContextRegistry.Clear(CopilotMeasurementResultAgentExtension.SourceId);
        }

        private async Task<CopilotMeasurementResultContextSnapshot?> CaptureCopilotMeasurementResultSnapshotAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Dispatcher.CheckAccess())
            {
                return await Dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CaptureCopilotMeasurementResultSnapshot();
                });
            }

            return CaptureCopilotMeasurementResultSnapshot();
        }

        private CopilotMeasurementResultContextSnapshot CaptureCopilotMeasurementResultSnapshot()
        {
            var selectedImage = _lastSelectedResultKind == "image" ? listView1.SelectedItem as ViewResultImage : null;
            var selectedAlgorithm = _lastSelectedResultKind == "algorithm" ? listView2.SelectedItem as ViewResultAlg : null;
            selectedImage ??= selectedAlgorithm == null ? listView1.SelectedItem as ViewResultImage : null;
            selectedAlgorithm ??= selectedImage == null ? listView2.SelectedItem as ViewResultAlg : null;
            var templateName = MeasureBatchModel.TId is int templateId
                ? Templates.Flow.TemplateFlow.Params.FirstOrDefault(item => item.Id == templateId)?.Key ?? string.Empty
                : string.Empty;

            return new CopilotMeasurementResultContextSnapshot
            {
                SourceId = CopilotMeasurementResultAgentExtension.SourceId,
                Surface = "Measurement batch details",
                LoadedBatchCount = 1,
                BatchId = MeasureBatchModel.Id,
                TemplateId = MeasureBatchModel.TId,
                TemplateName = templateName,
                BatchStatus = MeasureBatchModel.FlowStatus.ToString(),
                CreatedAt = MeasureBatchModel.CreateDate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                TotalTimeMilliseconds = MeasureBatchModel.TotalTime,
                ArchiveStatus = MeasureBatchModel.ArchiveStatus.ToString(),
                HasResultMessage = !string.IsNullOrWhiteSpace(MeasureBatchModel.Result),
                HasLoadedDetails = true,
                ImageResultCount = ViewResultImages.Count,
                FailedImageResultCount = ViewResultImages.Count(result => result.ResultCode != 0),
                AlgorithmResultCount = ViewResultAlgs.Count,
                FailedAlgorithmResultCount = ViewResultAlgs.Count(result => result.ResultCode.HasValue && result.ResultCode.Value != 0),
                UnknownAlgorithmResultCount = ViewResultAlgs.Count(result => !result.ResultCode.HasValue),
                SelectedResultKind = selectedImage != null ? "Image measurement" : selectedAlgorithm != null ? "Algorithm result" : string.Empty,
                SelectedResultId = selectedImage?.Id ?? selectedAlgorithm?.Id,
                SelectedResultType = selectedImage?.FileType.ToString() ?? selectedAlgorithm?.ResultType.ToString() ?? string.Empty,
                SelectedResultTemplateName = selectedAlgorithm?.POITemplateName ?? string.Empty,
                SelectedResultCode = selectedImage?.ResultCode.ToString(CultureInfo.InvariantCulture)
                    ?? selectedAlgorithm?.ResultCode?.ToString(CultureInfo.InvariantCulture)
                    ?? string.Empty,
                SelectedResultDuration = selectedImage?.TotalTime
                    ?? (selectedAlgorithm != null ? $"{selectedAlgorithm.TotalTime.ToString(CultureInfo.InvariantCulture)} ms" : string.Empty),
                SelectedResultCreatedAt = selectedImage?.CreateTime?.ToString("O", CultureInfo.InvariantCulture)
                    ?? selectedAlgorithm?.CreateTime?.ToString("O", CultureInfo.InvariantCulture)
                    ?? string.Empty,
                SelectedResultFileAvailable = selectedImage?.IsFileExists ?? selectedAlgorithm?.IsFileExists,
            };
        }

        private void PublishCopilotContext()
        {
            if (_copilotContextSession?.IsCurrent != true || _copilotHostWindow?.IsActive != true)
                return;

            var snapshot = CaptureCopilotMeasurementResultSnapshot();
            var item = CopilotBusinessContextBuilder.BuildMeasurementResultContextItem(snapshot);
            CopilotBusinessContextCoordinator.Publish(CopilotBusinessContextBundle.FromItem(
                CopilotMeasurementResultAgentExtension.SourceId,
                item));
        }
    }
}
