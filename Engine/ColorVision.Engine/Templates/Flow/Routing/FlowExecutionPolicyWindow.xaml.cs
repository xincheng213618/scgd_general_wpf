using FlowEngineLib.Base;
using FlowEngineLib.Runtime;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Flow.Routing
{
    public partial class FlowExecutionPolicyWindow :
        Window,
        INotifyPropertyChanged
    {
        private const int MaximumDelayMs = 86_400_000;

        private readonly IFlowExecutionPolicyStore store;
        private readonly Action<FlowExecutionPolicySnapshot>? savedCallback;
        private readonly Dictionary<string, STNode> nodesById;
        private FlowExecutionPolicySnapshot snapshot;
        private string statusText = string.Empty;
        private Brush statusBrush = Brushes.Gray;
        private bool canSave = true;

        public FlowExecutionPolicyWindow(
            FlowParam flowParam,
            IReadOnlyList<STNode> nodes,
            Action<FlowExecutionPolicySnapshot>? savedCallback = null)
        {
            ArgumentNullException.ThrowIfNull(flowParam);
            ArgumentNullException.ThrowIfNull(nodes);
            if (string.IsNullOrWhiteSpace(flowParam.FlowKey))
            {
                throw new ArgumentException(
                    "当前流程没有稳定 FlowKey，不能保存执行策略。",
                    nameof(flowParam));
            }

            FlowKey = flowParam.FlowKey.Trim();
            store = FlowExecutionPolicyStoreProvider.Shared;
            this.savedCallback = savedCallback;
            nodesById = nodes
                .Where(node => node != null && node.Guid != Guid.Empty)
                .GroupBy(
                    node => node.Guid.ToString("D"),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

            AllNodeChoices = new ObservableCollection<FlowPolicyNodeChoice>(
                nodesById.Values
                    .Select(CreateNodeChoice)
                    .OrderBy(
                        choice => choice.DisplayName,
                        StringComparer.CurrentCultureIgnoreCase));
            RetryNodeChoices =
                new ObservableCollection<FlowPolicyNodeChoice>(
                    AllNodeChoices.Where(choice =>
                        choice.Node is CVBaseServerNode));
            RouteSourceNodeChoices =
                new ObservableCollection<FlowPolicyNodeChoice>(
                    RetryNodeChoices);
            RouteTargetNodeChoices =
                new ObservableCollection<FlowPolicyNodeChoice>(
                    AllNodeChoices.Where(choice =>
                        choice.InputCount > 0));

            snapshot = CreateUnavailableSnapshot(FlowKey);
            InitializeComponent();
            DataContext = this;
            Title = $"流程执行策略 - {FlowKey}";
            Loaded += Window_Loaded;
            LoadPolicy();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string FlowKey { get; }

        public ObservableCollection<FlowPolicyNodeChoice> AllNodeChoices
        {
            get;
        }

        public ObservableCollection<FlowPolicyNodeChoice> RetryNodeChoices
        {
            get;
        }

        public ObservableCollection<FlowPolicyNodeChoice>
            RouteSourceNodeChoices { get; }

        public ObservableCollection<FlowPolicyNodeChoice>
            RouteTargetNodeChoices { get; }

        public ObservableCollection<FlowRetryPolicyRow> RetryRows
        {
            get;
        } = new();

        public ObservableCollection<FlowErrorRoutePolicyRow> RouteRows
        {
            get;
        } = new();

        public string RevisionText =>
            $"Policy revision: {snapshot.Revision}";

        public string StatusText
        {
            get => statusText;
            private set
            {
                if (statusText == value)
                    return;
                statusText = value;
                OnPropertyChanged();
            }
        }

        public Brush StatusBrush
        {
            get => statusBrush;
            private set
            {
                if (ReferenceEquals(statusBrush, value))
                    return;
                statusBrush = value;
                OnPropertyChanged();
            }
        }

        public bool CanSave
        {
            get => canSave;
            private set
            {
                if (canSave == value)
                    return;
                canSave = value;
                OnPropertyChanged();
            }
        }

        private void LoadPolicy()
        {
            if (!store.TryLoad(
                    FlowKey,
                    out FlowExecutionPolicySnapshot loaded,
                    out string? failureReason))
            {
                snapshot = loaded;
                RetryRows.Clear();
                RouteRows.Clear();
                CanSave = false;
                SetStatus(
                    $"读取失败：{failureReason}",
                    isError: true);
                OnPropertyChanged(nameof(RevisionText));
                return;
            }

            snapshot = loaded;
            RetryRows.Clear();
            RouteRows.Clear();
            foreach (FlowRetryPolicy policy in snapshot.RetryPolicies)
            {
                EnsureChoice(
                    RetryNodeChoices,
                    policy.NodeId,
                    "缺失或不支持重试的节点");
                RetryRows.Add(new FlowRetryPolicyRow(policy));
            }
            foreach (FlowErrorRoutePolicy route in snapshot.ErrorRoutes)
            {
                EnsureChoice(
                    RouteSourceNodeChoices,
                    route.SourceNodeId,
                    "缺失或不支持错误路由的来源节点");
                EnsureChoice(
                    RouteTargetNodeChoices,
                    route.TargetNodeId,
                    "缺失或没有输入端口的目标节点");
                RouteRows.Add(new FlowErrorRoutePolicyRow(route));
            }

            CanSave = true;
            SetStatus(
                snapshot.Revision == 0
                    ? "尚未创建执行策略侧车。"
                    : $"已读取 revision {snapshot.Revision}。",
                isError: false);
            OnPropertyChanged(nameof(RevisionText));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!CanSave)
            {
                MessageBox.Show(
                    this,
                    StatusText,
                    "执行策略读取失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AddRetry_Click(
            object sender,
            RoutedEventArgs e)
        {
            FlowPolicyNodeChoice? available =
                RetryNodeChoices.FirstOrDefault(choice =>
                    !choice.IsMissing
                    && RetryRows.All(row => !string.Equals(
                        row.NodeId,
                        choice.NodeId,
                        StringComparison.OrdinalIgnoreCase)));
            if (available == null)
            {
                ShowValidationMessage(
                    "没有可新增的服务节点，或所有服务节点都已配置重试策略。");
                return;
            }

            RetryRows.Add(FlowRetryPolicyRow.CreateDefault(
                available.NodeId));
            RetryGrid.SelectedItem = RetryRows[^1];
            RetryGrid.ScrollIntoView(RetryRows[^1]);
        }

        private void RemoveRetry_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (RetryGrid.SelectedItem is FlowRetryPolicyRow row)
                RetryRows.Remove(row);
        }

        private void AddRoute_Click(
            object sender,
            RoutedEventArgs e)
        {
            FlowPolicyNodeChoice? source =
                RouteSourceNodeChoices.FirstOrDefault(
                    choice => !choice.IsMissing);
            FlowPolicyNodeChoice? target =
                RouteTargetNodeChoices.FirstOrDefault(choice =>
                    !choice.IsMissing
                    && !string.Equals(
                        choice.NodeId,
                        source?.NodeId,
                        StringComparison.OrdinalIgnoreCase));
            if (source == null || target == null)
            {
                ShowValidationMessage(
                    "至少需要一个服务来源节点和一个带输入端口的其他目标节点。");
                return;
            }

            RouteRows.Add(FlowErrorRoutePolicyRow.CreateDefault(
                source.NodeId,
                target.NodeId));
            RouteGrid.SelectedItem = RouteRows[^1];
            RouteGrid.ScrollIntoView(RouteRows[^1]);
        }

        private void RemoveRoute_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (RouteGrid.SelectedItem is FlowErrorRoutePolicyRow row)
                RouteRows.Remove(row);
        }

        private void Reload_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                this,
                "重新读取会丢弃窗口中尚未保存的修改，是否继续？",
                "重新读取执行策略",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
                LoadPolicy();
        }

        private void Save_Click(
            object sender,
            RoutedEventArgs e)
        {
            CommitPendingEdits();
            if (HasValidationError(RetryGrid)
                || HasValidationError(RouteGrid))
            {
                ShowValidationMessage(
                    "表格中存在无法解析的数值，请先修正红色标记的单元格。");
                return;
            }

            try
            {
                IReadOnlyList<FlowRetryPolicy> retries =
                    BuildRetryPolicies();
                IReadOnlyList<FlowErrorRoutePolicy> routes =
                    BuildErrorRoutes();
                FlowExecutionPolicySnapshot saved = store.Save(
                    new FlowExecutionPolicySaveRequest(
                        FlowKey,
                        snapshot.Revision,
                        routes,
                        retries));
                snapshot = saved;
                OnPropertyChanged(nameof(RevisionText));
                SetStatus(
                    $"已保存 revision {saved.Revision}。",
                    isError: false);

                try
                {
                    savedCallback?.Invoke(saved);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        "策略已经保存，但通知调用者重新加载时失败："
                        + ex.Message,
                        "执行策略已保存",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                DialogResult = true;
            }
            catch (FlowExecutionPolicyConflictException ex)
            {
                SetStatus(ex.Message, isError: true);
                MessageBox.Show(
                    this,
                    ex.Message
                    + Environment.NewLine
                    + "请重新读取后再合并修改，当前内容不会覆盖新版本。",
                    "执行策略已被其他窗口修改",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
                when (ex is ArgumentException
                    || ex is InvalidOperationException
                    || ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is OverflowException)
            {
                SetStatus(ex.Message, isError: true);
                ShowValidationMessage(ex.Message);
            }
        }

        private List<FlowRetryPolicy> BuildRetryPolicies()
        {
            var policies = new List<FlowRetryPolicy>(RetryRows.Count);
            var usedNodes =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < RetryRows.Count; i++)
            {
                FlowRetryPolicyRow row = RetryRows[i];
                STNode node = RequireNode(
                    row.NodeId,
                    $"重试策略第 {i + 1} 行");
                if (node is not CVBaseServerNode)
                {
                    throw new InvalidOperationException(
                        $"重试策略第 {i + 1} 行的节点不支持运行时重试。");
                }
                if (!usedNodes.Add(row.NodeId))
                {
                    throw new InvalidOperationException(
                        $"重试策略第 {i + 1} 行与前面的节点重复。");
                }
                if (row.MaxAttempts < 1 || row.MaxAttempts > 100)
                {
                    throw new InvalidOperationException(
                        $"重试策略第 {i + 1} 行的 MaxAttempts "
                        + "必须介于 1 和 100。");
                }
                if (row.InitialDelayMs < 0
                    || row.InitialDelayMs > MaximumDelayMs)
                {
                    throw new InvalidOperationException(
                        $"重试策略第 {i + 1} 行的 InitialDelayMs "
                        + $"必须介于 0 和 {MaximumDelayMs}。");
                }
                if (!double.IsFinite(row.Backoff)
                    || row.Backoff < 1
                    || row.Backoff > 100)
                {
                    throw new InvalidOperationException(
                        $"重试策略第 {i + 1} 行的 Backoff "
                        + "必须介于 1 和 100。");
                }
                if (row.MaxDelayMs < row.InitialDelayMs
                    || row.MaxDelayMs > MaximumDelayMs)
                {
                    throw new InvalidOperationException(
                        $"重试策略第 {i + 1} 行的 MaxDelayMs "
                        + "不能小于 InitialDelayMs，且不能超过 "
                        + MaximumDelayMs + "。");
                }

                FlowFailureKind[] failureKinds =
                    row.GetSelectedFailureKinds();
                if (failureKinds.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"重试策略第 {i + 1} 行至少选择一种失败类型。");
                }
                if (failureKinds.Contains(FlowFailureKind.Canceled))
                {
                    throw new InvalidOperationException(
                        "Canceled 不能配置为自动重试。");
                }

                policies.Add(new FlowRetryPolicy(
                    row.NodeId,
                    row.MaxAttempts,
                    row.InitialDelayMs,
                    row.Backoff,
                    row.MaxDelayMs,
                    failureKinds));
            }
            return policies;
        }

        private List<FlowErrorRoutePolicy> BuildErrorRoutes()
        {
            var routes = new List<FlowErrorRoutePolicy>(RouteRows.Count);
            var bindings =
                new HashSet<(string NodeId, FlowFailureKind Kind)>();
            for (int i = 0; i < RouteRows.Count; i++)
            {
                FlowErrorRoutePolicyRow row = RouteRows[i];
                STNode source = RequireNode(
                    row.SourceNodeId,
                    $"ERROR 路由第 {i + 1} 行来源");
                STNode target = RequireNode(
                    row.TargetNodeId,
                    $"ERROR 路由第 {i + 1} 行目标");
                if (source is not CVBaseServerNode)
                {
                    throw new InvalidOperationException(
                        $"ERROR 路由第 {i + 1} 行的来源节点"
                        + "不支持运行时错误路由。");
                }
                if (string.Equals(
                    row.SourceNodeId,
                    row.TargetNodeId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"ERROR 路由第 {i + 1} 行不能指向来源节点自身。");
                }

                int inputCount = target.GetAllInputOptions().Length;
                if (row.TargetInputIndex < 0
                    || row.TargetInputIndex >= inputCount)
                {
                    throw new InvalidOperationException(
                        $"ERROR 路由第 {i + 1} 行的目标输入索引 "
                        + $"{row.TargetInputIndex} 无效；目标节点共有 "
                        + $"{inputCount} 个输入端口。");
                }

                FlowFailureKind[] failureKinds =
                    row.GetSelectedFailureKinds();
                if (failureKinds.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"ERROR 路由第 {i + 1} 行至少选择一种失败类型。");
                }
                foreach (FlowFailureKind kind in failureKinds)
                {
                    if (!bindings.Add((row.SourceNodeId, kind)))
                    {
                        throw new InvalidOperationException(
                            $"来源节点“{GetNodeDisplayName(row.SourceNodeId)}”"
                            + $"的 {GetFailureKindDisplayName(kind)} "
                            + "已配置其他 ERROR 路由。");
                    }
                }

                routes.Add(new FlowErrorRoutePolicy(
                    row.SourceNodeId,
                    row.TargetNodeId,
                    row.TargetInputIndex,
                    failureKinds));
            }
            return routes;
        }

        private STNode RequireNode(
            string? nodeId,
            string location)
        {
            if (string.IsNullOrWhiteSpace(nodeId)
                || !nodesById.TryGetValue(nodeId, out STNode? node))
            {
                throw new InvalidOperationException(
                    $"{location}引用的节点已不存在，请重新选择。");
            }
            return node;
        }

        private string GetNodeDisplayName(string nodeId)
        {
            return nodesById.TryGetValue(nodeId, out STNode? node)
                ? CreateNodeChoice(node).DisplayName
                : nodeId;
        }

        private static void EnsureChoice(
            ObservableCollection<FlowPolicyNodeChoice> choices,
            string nodeId,
            string reason)
        {
            if (choices.Any(choice => string.Equals(
                choice.NodeId,
                nodeId,
                StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            choices.Add(new FlowPolicyNodeChoice(
                nodeId,
                $"⚠ {reason} · {ShortNodeId(nodeId)}",
                node: null,
                inputCount: 0,
                isMissing: true));
        }

        private static FlowPolicyNodeChoice CreateNodeChoice(STNode node)
        {
            string title = string.IsNullOrWhiteSpace(node.Title)
                ? node.GetType().Name
                : node.Title.Trim();
            var details = new List<string>();
            if (node is CVCommonNode commonNode)
            {
                if (!string.IsNullOrWhiteSpace(commonNode.NodeName)
                    && !string.Equals(
                        commonNode.NodeName.Trim(),
                        title,
                        StringComparison.OrdinalIgnoreCase))
                {
                    details.Add(commonNode.NodeName.Trim());
                }
                if (!string.IsNullOrWhiteSpace(commonNode.DeviceCode))
                    details.Add(commonNode.DeviceCode.Trim());
            }

            string detailText = details.Count == 0
                ? string.Empty
                : $" · {string.Join(" / ", details)}";
            string nodeId = node.Guid.ToString("D");
            return new FlowPolicyNodeChoice(
                nodeId,
                $"{title}{detailText} · {ShortNodeId(nodeId)}",
                node,
                node.GetAllInputOptions().Length,
                isMissing: false);
        }

        private static string ShortNodeId(string nodeId)
        {
            return Guid.TryParse(nodeId, out Guid guid)
                ? guid.ToString("N")[..8]
                : nodeId;
        }

        private static string GetFailureKindDisplayName(
            FlowFailureKind kind)
        {
            return kind switch
            {
                FlowFailureKind.Business => "业务错误",
                FlowFailureKind.Technical => "技术错误",
                FlowFailureKind.Timeout => "超时",
                FlowFailureKind.Canceled => "取消",
                FlowFailureKind.Contract => "契约错误",
                _ => kind.ToString(),
            };
        }

        private void CommitPendingEdits()
        {
            RetryGrid.CommitEdit(
                DataGridEditingUnit.Cell,
                exitEditingMode: true);
            RetryGrid.CommitEdit(
                DataGridEditingUnit.Row,
                exitEditingMode: true);
            RouteGrid.CommitEdit(
                DataGridEditingUnit.Cell,
                exitEditingMode: true);
            RouteGrid.CommitEdit(
                DataGridEditingUnit.Row,
                exitEditingMode: true);
        }

        private static bool HasValidationError(
            DependencyObject element)
        {
            if (Validation.GetHasError(element))
                return true;
            int childCount =
                VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                if (HasValidationError(
                    VisualTreeHelper.GetChild(element, i)))
                {
                    return true;
                }
            }
            return false;
        }

        private void ShowValidationMessage(string message)
        {
            SetStatus(message, isError: true);
            MessageBox.Show(
                this,
                message,
                "执行策略校验失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void SetStatus(
            string message,
            bool isError)
        {
            StatusText = message;
            StatusBrush = isError
                ? Brushes.IndianRed
                : Brushes.Gray;
        }

        private static FlowExecutionPolicySnapshot
            CreateUnavailableSnapshot(string flowKey)
        {
            return new FlowExecutionPolicySnapshot(
                flowKey,
                revision: 0,
                contentHash: string.Empty,
                updatedTimeUtc: DateTime.UnixEpoch,
                Array.Empty<FlowErrorRoutePolicy>(),
                Array.Empty<FlowRetryPolicy>());
        }

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class FlowPolicyNodeChoice
    {
        internal FlowPolicyNodeChoice(
            string nodeId,
            string displayName,
            STNode? node,
            int inputCount,
            bool isMissing)
        {
            NodeId = nodeId;
            DisplayName = displayName;
            Node = node;
            InputCount = inputCount;
            IsMissing = isMissing;
        }

        public string NodeId { get; }

        public string DisplayName { get; }

        public int InputCount { get; }

        public bool IsMissing { get; }

        internal STNode? Node { get; }
    }

    public sealed class FlowFailureKindSelection :
        INotifyPropertyChanged
    {
        private bool isSelected;

        internal FlowFailureKindSelection(
            FlowFailureKind kind,
            bool isSelected)
        {
            Kind = kind;
            DisplayName = kind switch
            {
                FlowFailureKind.Business => "业务",
                FlowFailureKind.Technical => "技术",
                FlowFailureKind.Timeout => "超时",
                FlowFailureKind.Canceled => "取消",
                FlowFailureKind.Contract => "契约",
                _ => kind.ToString(),
            };
            this.isSelected = isSelected;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public FlowFailureKind Kind { get; }

        public string DisplayName { get; }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value)
                    return;
                isSelected = value;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public sealed class FlowRetryPolicyRow
    {
        private static readonly FlowFailureKind[] SupportedKinds =
        [
            FlowFailureKind.Business,
            FlowFailureKind.Technical,
            FlowFailureKind.Timeout,
            FlowFailureKind.Contract,
        ];

        internal FlowRetryPolicyRow(FlowRetryPolicy policy)
            : this(
                policy.NodeId,
                policy.MaxAttempts,
                policy.InitialDelayMs,
                policy.Backoff,
                policy.MaxDelayMs,
                policy.RetryableKinds)
        {
        }

        private FlowRetryPolicyRow(
            string nodeId,
            int maxAttempts,
            int initialDelayMs,
            double backoff,
            int maxDelayMs,
            IReadOnlyCollection<FlowFailureKind> selectedKinds)
        {
            NodeId = nodeId;
            MaxAttempts = maxAttempts;
            InitialDelayMs = initialDelayMs;
            Backoff = backoff;
            MaxDelayMs = maxDelayMs;
            FailureKinds = new ObservableCollection<
                FlowFailureKindSelection>(
                SupportedKinds.Select(kind =>
                    new FlowFailureKindSelection(
                        kind,
                        selectedKinds.Contains(kind))));
        }

        public string NodeId { get; set; }

        public int MaxAttempts { get; set; }

        public int InitialDelayMs { get; set; }

        public double Backoff { get; set; }

        public int MaxDelayMs { get; set; }

        public ObservableCollection<FlowFailureKindSelection> FailureKinds
        {
            get;
        }

        internal static FlowRetryPolicyRow CreateDefault(string nodeId)
        {
            return new FlowRetryPolicyRow(
                nodeId,
                maxAttempts: 3,
                initialDelayMs: 500,
                backoff: 2,
                maxDelayMs: 5_000,
                [
                    FlowFailureKind.Technical,
                    FlowFailureKind.Timeout,
                ]);
        }

        internal FlowFailureKind[] GetSelectedFailureKinds()
        {
            return FailureKinds
                .Where(item => item.IsSelected)
                .Select(item => item.Kind)
                .ToArray();
        }
    }

    public sealed class FlowErrorRoutePolicyRow
    {
        private static readonly FlowFailureKind[] SupportedKinds =
            Enum.GetValues<FlowFailureKind>();

        internal FlowErrorRoutePolicyRow(FlowErrorRoutePolicy policy)
            : this(
                policy.SourceNodeId,
                policy.TargetNodeId,
                policy.TargetInputIndex,
                policy.FailureKinds)
        {
        }

        private FlowErrorRoutePolicyRow(
            string sourceNodeId,
            string targetNodeId,
            int targetInputIndex,
            IReadOnlyCollection<FlowFailureKind> selectedKinds)
        {
            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
            TargetInputIndex = targetInputIndex;
            FailureKinds = new ObservableCollection<
                FlowFailureKindSelection>(
                SupportedKinds.Select(kind =>
                    new FlowFailureKindSelection(
                        kind,
                        selectedKinds.Contains(kind))));
        }

        public string SourceNodeId { get; set; }

        public string TargetNodeId { get; set; }

        public int TargetInputIndex { get; set; }

        public ObservableCollection<FlowFailureKindSelection> FailureKinds
        {
            get;
        }

        internal static FlowErrorRoutePolicyRow CreateDefault(
            string sourceNodeId,
            string targetNodeId)
        {
            return new FlowErrorRoutePolicyRow(
                sourceNodeId,
                targetNodeId,
                targetInputIndex: 0,
                [
                    FlowFailureKind.Business,
                    FlowFailureKind.Technical,
                    FlowFailureKind.Contract,
                ]);
        }

        internal FlowFailureKind[] GetSelectedFailureKinds()
        {
            return FailureKinds
                .Where(item => item.IsSelected)
                .Select(item => item.Kind)
                .ToArray();
        }
    }
}
