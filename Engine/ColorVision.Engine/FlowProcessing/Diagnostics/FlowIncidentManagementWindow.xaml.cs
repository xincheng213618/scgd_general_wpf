using ColorVision.Themes;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal partial class FlowIncidentManagementWindow : Window
    {
        private readonly Func<FlowIncidentDetail, bool>? focusFlowNode;
        private readonly bool autoLoad;
        private FlowIncidentService? service;
        private FlowIncidentPage? currentPage;
        private FlowIncidentDetail? selectedDetail;
        private int pageNumber = 1;
        private bool isBusy;

        internal FlowIncidentManagementWindow(
            Func<FlowIncidentDetail, bool>? focusFlowNode = null,
            bool autoLoad = true)
        {
            this.focusFlowNode = focusFlowNode;
            this.autoLoad = autoLoad;
            InitializeComponent();
            this.ApplyCaption();
            OperatorTextBox.Text = Environment.UserName;
            UpdateActionButtons();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            if (autoLoad)
                await RefreshAsync(resetPage: true);
        }

        private async Task RefreshAsync(
            bool resetPage,
            long? preferredIncidentId = null)
        {
            if (isBusy)
                return;
            if (resetPage)
                pageNumber = 1;

            SetBusy(true);
            try
            {
                service ??= await Task.Run(() => new FlowIncidentService());
                FlowIncidentQuery query = CreateQuery();
                FlowIncidentPage result =
                    await Task.Run(() => service.Query(query));
                if (result.Items.Count == 0
                    && result.TotalCount > 0
                    && result.PageNumber > result.TotalPages)
                {
                    pageNumber = result.TotalPages;
                    query = CreateQuery();
                    result = await Task.Run(() => service.Query(query));
                }
                currentPage = result;
                pageNumber = Math.Min(result.PageNumber, result.TotalPages);
                IncidentDataGrid.ItemsSource = result.Items;
                PageStatusText.Text =
                    $"第 {pageNumber} / {result.TotalPages} 页，共 {result.TotalCount} 条";
                PreviousPageButton.IsEnabled = pageNumber > 1;
                NextPageButton.IsEnabled = pageNumber < result.TotalPages;

                FlowIncidentListItem? preferred = null;
                if (preferredIncidentId.HasValue)
                {
                    foreach (FlowIncidentListItem item in result.Items)
                    {
                        if (item.IncidentId == preferredIncidentId.Value)
                        {
                            preferred = item;
                            break;
                        }
                    }
                }
                IncidentDataGrid.SelectedItem =
                    preferred
                    ?? (result.Items.Count > 0 ? result.Items[0] : null);
                if (result.Items.Count == 0)
                    ClearDetail();
            }
            catch (Exception ex)
            {
                ShowError("读取 Incident 失败", ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private FlowIncidentQuery CreateQuery()
        {
            string state =
                (StateFilterComboBox.SelectedItem as ComboBoxItem)?.Tag
                    ?.ToString()
                ?? FlowIncidentStates.Active;
            return new FlowIncidentQuery
            {
                State = state,
                Severity = SeverityFilterTextBox.Text,
                Kind = KindFilterTextBox.Text,
                SearchText = SearchTextBox.Text,
                PageNumber = pageNumber,
                PageSize = 50,
            };
        }

        private async void IncidentDataGrid_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (IncidentDataGrid.SelectedItem
                is not FlowIncidentListItem selected
                || service == null)
            {
                ClearDetail();
                return;
            }

            try
            {
                FlowIncidentDetail detail =
                    await Task.Run(
                        () => service.GetDetail(selected.IncidentId));
                if (IncidentDataGrid.SelectedItem
                        is not FlowIncidentListItem current
                    || current.IncidentId != selected.IncidentId)
                {
                    return;
                }

                selectedDetail = detail;
                DataContext = detail;
                EventDataGrid.ItemsSource = detail.Events;
                AttemptDataGrid.ItemsSource = detail.Attempts;
                UpdateActionButtons();
            }
            catch (Exception ex)
            {
                ClearDetail();
                ShowError("读取 Incident 详情失败", ex);
            }
        }

        private async void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RefreshAsync(resetPage: true);
        }

        private async void SearchTextBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            e.Handled = true;
            await RefreshAsync(resetPage: true);
        }

        private async void PreviousPageButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (pageNumber <= 1)
                return;
            --pageNumber;
            await RefreshAsync(resetPage: false);
        }

        private async void NextPageButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (currentPage == null
                || pageNumber >= currentPage.TotalPages)
            {
                return;
            }
            ++pageNumber;
            await RefreshAsync(resetPage: false);
        }

        private async void AcknowledgeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (selectedDetail == null || service == null || isBusy)
                return;

            long incidentId = selectedDetail.Incident.Id;
            SetBusy(true);
            try
            {
                await Task.Run(() =>
                    service.Acknowledge(
                        incidentId,
                        OperatorTextBox.Text,
                        ActionNoteTextBox.Text));
                ActionNoteTextBox.Clear();
            }
            catch (Exception ex)
            {
                ShowError("确认 Incident 失败", ex);
                return;
            }
            finally
            {
                SetBusy(false);
            }
            await RefreshAsync(resetPage: false, incidentId);
        }

        private async void ResolveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (selectedDetail == null || service == null || isBusy)
                return;
            if (string.IsNullOrWhiteSpace(ActionNoteTextBox.Text))
            {
                MessageBox.Show(
                    this,
                    "关闭 Incident 前请填写处置结果。",
                    "流程 Incident",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                ActionNoteTextBox.Focus();
                return;
            }

            long incidentId = selectedDetail.Incident.Id;
            SetBusy(true);
            try
            {
                await Task.Run(() =>
                    service.Resolve(
                        incidentId,
                        OperatorTextBox.Text,
                        ActionNoteTextBox.Text));
                ActionNoteTextBox.Clear();
            }
            catch (Exception ex)
            {
                ShowError("关闭 Incident 失败", ex);
                return;
            }
            finally
            {
                SetBusy(false);
            }
            await RefreshAsync(resetPage: false, incidentId);
        }

        private void OpenRunAnalysisButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (selectedDetail?.Run?.BatchId is not > 0)
            {
                MessageBox.Show(
                    this,
                    "该运行没有关联旧版 Batch 记录，无法打开节点耗时分析。"
                        + Environment.NewLine
                        + BuildStableIdentifiers(),
                    "流程 Incident",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                var window = new FlowExecutionAnalysisWindow(
                    selectedDetail.Run.BatchId.Value,
                    selectedDetail.Run.SerialNumber,
                    selectedDetail.Incident.NodeId)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };
                window.Show();
            }
            catch (Exception ex)
            {
                ShowError("打开运行分析失败", ex);
            }
        }

        private void LocateFlowNodeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (selectedDetail != null
                    && focusFlowNode?.Invoke(selectedDetail) == true)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowError("定位流程节点失败", ex);
                return;
            }

            MessageBox.Show(
                this,
                "当前画布不是该 Incident 对应流程，或节点已不在当前版本中。"
                    + Environment.NewLine
                    + BuildStableIdentifiers(),
                "流程 Incident",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CopyIdentifiersButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (selectedDetail == null)
                return;
            try
            {
                ColorVision.Common.Clipboard.SetText(BuildStableIdentifiers());
            }
            catch (Exception ex)
            {
                ShowError("复制稳定标识失败", ex);
            }
        }

        private string BuildStableIdentifiers()
        {
            if (selectedDetail == null)
                return "未选择 Incident。";
            return $"IncidentId={selectedDetail.Incident.Id}"
                + Environment.NewLine
                + $"RunRecordId={selectedDetail.Incident.RunRecordId}"
                + Environment.NewLine
                + $"RunKey={selectedDetail.Run?.RunKey ?? "—"}"
                + Environment.NewLine
                + $"FlowKey={selectedDetail.Run?.FlowKey ?? "—"}"
                + Environment.NewLine
                + $"TemplateId={selectedDetail.Run?.TemplateId.ToString() ?? "—"}"
                + Environment.NewLine
                + $"NodeId={selectedDetail.Incident.NodeId ?? "—"}";
        }

        private void ClearDetail()
        {
            selectedDetail = null;
            DataContext = null;
            EventDataGrid.ItemsSource = null;
            AttemptDataGrid.ItemsSource = null;
            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            string? state = selectedDetail?.Incident.State;
            bool canAcknowledge =
                string.Equals(
                    state,
                    FlowIncidentStates.Open,
                    StringComparison.Ordinal);
            bool canResolve =
                canAcknowledge
                || string.Equals(
                    state,
                    FlowIncidentStates.Acknowledged,
                    StringComparison.Ordinal);
            AcknowledgeButton.IsEnabled = !isBusy && canAcknowledge;
            ResolveButton.IsEnabled = !isBusy && canResolve;
            OpenRunAnalysisButton.IsEnabled =
                !isBusy && selectedDetail != null;
            LocateFlowNodeButton.IsEnabled =
                !isBusy && selectedDetail != null;
        }

        private void SetBusy(bool value)
        {
            isBusy = value;
            Mouse.OverrideCursor = value ? Cursors.Wait : null;
            UpdateActionButtons();
        }

        private void ShowError(string title, Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        protected override void OnClosed(EventArgs e)
        {
            service?.Dispose();
            service = null;
            Mouse.OverrideCursor = null;
            base.OnClosed(e);
        }
    }
}
