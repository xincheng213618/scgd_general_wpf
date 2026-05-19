using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace ColorVision.Copilot
{
    public partial class CopilotExceptionWindow : Window
    {
        private readonly CopilotExceptionViewModel _viewModel;

        public string ExceptionFingerprint => _viewModel.ExceptionFingerprint;

        public CopilotExceptionWindow(Exception exception, string source)
        {
            _viewModel = CopilotExceptionViewModel.Create(exception, source);

            InitializeComponent();
            this.ApplyCaption();
            DataContext = _viewModel;

            RecentLineCountTextBox.Text = _viewModel.RecentLineCount.ToString();
            UpdateLogModeUi();
        }

        public static void ShowException(Exception exception, string source)
        {
            if (exception == null)
                return;

            var window = new CopilotExceptionWindow(exception, source);
            var owner = Application.Current?.GetActiveWindow();
            if (owner != null)
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            window.Show();
            window.Activate();
        }

        public void RegisterDuplicateOccurrence(Exception exception, string source)
        {
            _viewModel.RegisterDuplicateOccurrence(exception, source);
            BringToFront();
        }

        public void RegisterAdditionalException(Exception exception, string source)
        {
            _viewModel.RegisterAdditionalException(exception, source);
            BringToFront();
        }

        public void BringToFront()
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void CopyDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_viewModel.BuildClipboardText());
                _viewModel.DispatchStatus = "异常详情已复制到剪贴板。";
            }
            catch (Exception ex)
            {
                _viewModel.DispatchStatus = $"复制失败：{ex.Message}";
            }
        }

        private void AskAiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = CopilotPanelService.GetInstance().DispatchExceptionPrompt(_viewModel.AiPromptPreview);
                _viewModel.DispatchStatus = result.StatusMessage;
            }
            catch (Exception ex)
            {
                _viewModel.DispatchStatus = $"发送到 AI 失败：{ex.Message}";
            }
        }

        private void SearchGoogleButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_viewModel.GoogleSearchUrl))
            {
                _viewModel.DispatchStatus = "当前异常没有可搜索的关键词。";
                return;
            }

            PlatformHelper.Open(_viewModel.GoogleSearchUrl);
            _viewModel.DispatchStatus = "已在默认浏览器中打开 Google 搜索结果。";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RefreshLogButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyLogSelectionFromUi();
        }

        private void RecentLinesRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateLogModeUi();
        }

        private void FullDayRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateLogModeUi();
        }

        private void ApplyLogSelectionFromUi()
        {
            if (RecentLineCountTextBox == null || FullDayRadioButton == null)
                return;

            var mode = FullDayRadioButton.IsChecked == true
                ? CopilotRecentLogMode.FullDay
                : CopilotRecentLogMode.RecentLines;

            if (int.TryParse(RecentLineCountTextBox.Text, out var recentLineCount))
            {
                _viewModel.ApplyLogOptions(mode, recentLineCount);
                RecentLineCountTextBox.Text = _viewModel.RecentLineCount.ToString();
                return;
            }

            _viewModel.ApplyLogOptions(mode, _viewModel.RecentLineCount, showValidationMessage: true);
            RecentLineCountTextBox.Text = _viewModel.RecentLineCount.ToString();
        }

        private void UpdateLogModeUi()
        {
            if (RecentLineCountTextBox == null || RecentLinesRadioButton == null || FullDayRadioButton == null)
                return;

            var isRecentLines = RecentLinesRadioButton.IsChecked != false;
            RecentLineCountTextBox.IsEnabled = isRecentLines;

            if (_viewModel.CurrentLogMode != (isRecentLines ? CopilotRecentLogMode.RecentLines : CopilotRecentLogMode.FullDay))
                ApplyLogSelectionFromUi();
        }
    }

    internal sealed class CopilotExceptionViewModel : ViewModelBase
    {
        private const int DefaultRecentLineCount = 160;
        private const int MinimumRecentLineCount = 20;
        private const int MaximumRecentLineCount = 2000;

        private readonly string _initialSource;
        private readonly DateTime _firstOccurredAt;
        private readonly List<string> _exceptionSections = new();
        private int _repeatCount = 1;
        private int _additionalExceptionCount;

        private CopilotExceptionViewModel(Exception exception, string source)
        {
            _initialSource = source ?? string.Empty;
            _firstOccurredAt = DateTime.Now;
            ExceptionFingerprint = BuildFingerprint(exception);
            CanAskAi = CopilotPanelService.GetInstance().CanAskFromException;
            ExceptionTitle = BuildExceptionTitle(exception);
            GoogleSearchUrl = BuildGoogleSearchUrl(exception);
            _exceptionSections.Add(BuildExceptionDetails(exception, source, _firstOccurredAt));
            ExceptionDetails = _exceptionSections[0];
            CurrentLogMode = CopilotRecentLogMode.RecentLines;
            RecentLineCount = DefaultRecentLineCount;
            UpdateOccurredSummary();
            RefreshLogSnapshot();
            DispatchStatus = CanAskAi
                ? "可以把当前异常与最近日志直接发送到主界面的 AI 视图。"
                : "当前 AI 未配置或主界面尚未就绪，暂时不能直接询问。";
        }

        public string ExceptionFingerprint
        {
            get => _exceptionFingerprint;
            private set => SetProperty(ref _exceptionFingerprint, value ?? string.Empty);
        }
        private string _exceptionFingerprint = string.Empty;

        public string ExceptionTitle
        {
            get => _exceptionTitle;
            set => SetProperty(ref _exceptionTitle, value ?? string.Empty);
        }
        private string _exceptionTitle = string.Empty;

        public string OccurredSummary
        {
            get => _occurredSummary;
            set => SetProperty(ref _occurredSummary, value ?? string.Empty);
        }
        private string _occurredSummary = string.Empty;

        public string ExceptionDetails
        {
            get => _exceptionDetails;
            set => SetProperty(ref _exceptionDetails, value ?? string.Empty);
        }
        private string _exceptionDetails = string.Empty;

        public string RecentLogHeader
        {
            get => _recentLogHeader;
            set => SetProperty(ref _recentLogHeader, value ?? string.Empty);
        }
        private string _recentLogHeader = string.Empty;

        public string RecentLogContent
        {
            get => _recentLogContent;
            set => SetProperty(ref _recentLogContent, value ?? string.Empty);
        }
        private string _recentLogContent = string.Empty;

        public string AiPromptPreview
        {
            get => _aiPromptPreview;
            set => SetProperty(ref _aiPromptPreview, value ?? string.Empty);
        }
        private string _aiPromptPreview = string.Empty;

        public string GoogleSearchUrl
        {
            get => _googleSearchUrl;
            private set
            {
                if (SetProperty(ref _googleSearchUrl, value ?? string.Empty))
                    OnPropertyChanged(nameof(CanSearchGoogle));
            }
        }
        private string _googleSearchUrl = string.Empty;

        public bool CanSearchGoogle => !string.IsNullOrWhiteSpace(GoogleSearchUrl);

        public bool CanAskAi
        {
            get => _canAskAi;
            set => SetProperty(ref _canAskAi, value);
        }
        private bool _canAskAi;

        public string DispatchStatus
        {
            get => _dispatchStatus;
            set => SetProperty(ref _dispatchStatus, value ?? string.Empty);
        }
        private string _dispatchStatus = string.Empty;

        public CopilotRecentLogMode CurrentLogMode
        {
            get => _currentLogMode;
            private set => SetProperty(ref _currentLogMode, value);
        }
        private CopilotRecentLogMode _currentLogMode;

        public int RecentLineCount
        {
            get => _recentLineCount;
            private set => SetProperty(ref _recentLineCount, value);
        }
        private int _recentLineCount = DefaultRecentLineCount;

        public static CopilotExceptionViewModel Create(Exception exception, string source)
        {
            return new CopilotExceptionViewModel(exception, source);
        }

        public void ApplyLogOptions(CopilotRecentLogMode mode, int requestedRecentLineCount, bool showValidationMessage = false)
        {
            CurrentLogMode = mode;
            RecentLineCount = NormalizeRecentLineCount(requestedRecentLineCount);
            RefreshLogSnapshot();

            if (showValidationMessage)
                DispatchStatus = $"最近行数输入无效，已恢复为 {RecentLineCount} 行。";
        }

        public void RegisterDuplicateOccurrence(Exception exception, string source)
        {
            _repeatCount++;
            UpdateOccurredSummary();
            RefreshLogSnapshot();
            DispatchStatus = $"检测到同类异常重复 {_repeatCount} 次，已合并到当前窗口。";
        }

        public void RegisterAdditionalException(Exception exception, string source)
        {
            _additionalExceptionCount++;
            _exceptionSections.Add(BuildExceptionDetails(exception, $"{source} [附加异常 {_additionalExceptionCount}]", DateTime.Now));
            ExceptionDetails = string.Join(Environment.NewLine + Environment.NewLine + "----------------" + Environment.NewLine + Environment.NewLine, _exceptionSections);
            UpdateOccurredSummary();
            RefreshLogSnapshot();
            DispatchStatus = $"短时间内又捕获到 {_additionalExceptionCount} 个额外异常，已合并到当前窗口。";
        }

        public string BuildClipboardText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("[异常详情]");
            builder.AppendLine(ExceptionDetails.Trim());

            builder.AppendLine();
            builder.AppendLine("[最近日志]");
            builder.AppendLine(RecentLogHeader.Trim());
            builder.AppendLine(RecentLogContent.Trim());

            builder.AppendLine();
            builder.AppendLine("[AI 请求]");
            builder.Append(AiPromptPreview.Trim());
            return builder.ToString().TrimEnd();
        }

        private static string BuildExceptionTitle(Exception exception)
        {
            var message = string.IsNullOrWhiteSpace(exception.Message) ? "未提供异常消息" : exception.Message.Trim();
            return $"{exception.GetType().Name}: {message}";
        }

        private static string BuildExceptionDetails(Exception exception, string source, DateTime occurredAt)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"捕获来源：{source}");
            builder.AppendLine($"发生时间：{occurredAt:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine();

            AppendException(builder, exception, 0);
            return builder.ToString().TrimEnd();
        }

        private static string BuildGoogleSearchUrl(Exception exception)
        {
            var searchTerms = new List<string>();
            AddSearchTerm(searchTerms, "ColorVision", 0);
            AddSearchTerm(searchTerms, exception.GetType().Name, 0);
            AddSearchTerm(searchTerms, exception.Message, 160);
            AddSearchTerm(searchTerms, exception.TargetSite?.DeclaringType?.Name, 0);
            AddSearchTerm(searchTerms, exception.TargetSite?.Name, 0);

            var query = string.Join(" ", searchTerms);
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            return $"https://www.google.com/search?hl=zh-CN&q={Uri.EscapeDataString(query)}";
        }

        private static void AddSearchTerm(List<string> searchTerms, string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var normalizedValue = value.Trim();
            if (maxLength > 0 && normalizedValue.Length > maxLength)
                normalizedValue = normalizedValue[..maxLength];

            searchTerms.Add(normalizedValue);
        }

        private void UpdateOccurredSummary()
        {
            var builder = new StringBuilder();
            builder.Append($"首次来源：{_initialSource}    时间：{_firstOccurredAt:yyyy-MM-dd HH:mm:ss}");

            if (_repeatCount > 1)
                builder.Append($"    重复：{_repeatCount} 次");

            if (_additionalExceptionCount > 0)
                builder.Append($"    附加异常：{_additionalExceptionCount} 个");

            OccurredSummary = builder.ToString();
        }

        private void RefreshLogSnapshot()
        {
            var maxChars = CurrentLogMode == CopilotRecentLogMode.FullDay
                ? CopilotRecentLogSupport.FullDayMaxLogChars
                : 12000;

            var snapshot = CopilotRecentLogSupport.Capture(
                mode: CurrentLogMode,
                maxLines: RecentLineCount,
                maxChars: maxChars);

            RecentLogHeader = snapshot.Success
                ? $"{snapshot.Summary}{Environment.NewLine}{snapshot.FilePath}"
                : snapshot.Summary;

            RecentLogContent = snapshot.Success
                ? snapshot.Content
                : string.IsNullOrWhiteSpace(snapshot.ErrorMessage) ? "未找到最近日志。" : snapshot.ErrorMessage;

            AiPromptPreview = BuildAiPrompt(OccurredSummary, ExceptionDetails, snapshot);
        }

        private static int NormalizeRecentLineCount(int requestedRecentLineCount)
        {
            return Math.Clamp(requestedRecentLineCount, MinimumRecentLineCount, MaximumRecentLineCount);
        }

        private static void AppendException(StringBuilder builder, Exception exception, int depth)
        {
            var indent = new string(' ', depth * 2);

            builder.Append(indent).Append("类型：").AppendLine(exception.GetType().FullName ?? exception.GetType().Name);
            builder.Append(indent).Append("消息：").AppendLine(exception.Message ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(exception.Source))
                builder.Append(indent).Append("Source：").AppendLine(exception.Source);

            if (exception.TargetSite != null)
            {
                var targetType = exception.TargetSite.DeclaringType?.FullName;
                var targetName = string.IsNullOrWhiteSpace(targetType)
                    ? exception.TargetSite.Name
                    : $"{targetType}.{exception.TargetSite.Name}";
                builder.Append(indent).Append("Target：").AppendLine(targetName);
            }

            if (!string.IsNullOrWhiteSpace(exception.StackTrace))
            {
                builder.Append(indent).AppendLine("堆栈：");
                builder.AppendLine(exception.StackTrace.Trim());
            }

            if (exception.InnerException == null)
                return;

            builder.AppendLine();
            builder.Append(indent).AppendLine("InnerException：");
            AppendException(builder, exception.InnerException, depth + 1);
        }

        private static string BuildAiPrompt(string occurrenceSummary, string exceptionDetails, CopilotRecentLogSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("请帮我分析 ColorVision WPF 客户端中的未处理异常。请基于异常内容和最近日志，给出最可能原因、优先排查点，以及最小可行修复方向；如果信息不足，请明确指出还缺什么。");
            builder.AppendLine();
            builder.AppendLine("[异常]");
            builder.AppendLine(exceptionDetails.Trim());
            builder.AppendLine();
            builder.AppendLine("[最近日志]");
            if (snapshot.Success)
            {
                builder.AppendLine($"日志文件：{snapshot.FilePath}");
                builder.AppendLine(snapshot.Content.Trim());
            }
            else
            {
                builder.AppendLine(string.IsNullOrWhiteSpace(snapshot.ErrorMessage) ? "未找到最近日志。" : snapshot.ErrorMessage.Trim());
            }

            builder.AppendLine();
            builder.AppendLine($"[补充说明] {occurrenceSummary}");
            return builder.ToString().TrimEnd();
        }

        private static string BuildFingerprint(Exception exception)
        {
            return string.Join("|", new[]
            {
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.Message ?? string.Empty,
                exception.Source ?? string.Empty,
                exception.TargetSite?.DeclaringType?.FullName ?? string.Empty,
                exception.TargetSite?.Name ?? string.Empty,
            });
        }
    }
}