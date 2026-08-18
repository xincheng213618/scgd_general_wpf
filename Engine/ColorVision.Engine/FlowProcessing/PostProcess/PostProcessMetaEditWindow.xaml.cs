using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.FlowProcessing.PostProcess
{
    public partial class PostProcessMetaEditWindow : Window
    {
        private readonly PostProcessMetaEditViewModel _viewModel;

        public string MetaName => _viewModel.MetaName.Trim();
        public string MetaTag => _viewModel.Tag.Trim();
        public TemplateModel<FlowParam>? SelectedTemplate => _viewModel.SelectedTemplate;
        public IPostProcessor? SelectedProcess => _viewModel.SelectedProcess;
        public PostProcessFailurePolicy FailurePolicy => _viewModel.FailurePolicy;

        public PostProcessMetaEditWindow(
            IEnumerable<TemplateModel<FlowParam>> templates,
            IEnumerable<IPostProcessor> processes,
            string title,
            string metaName = "",
            string templateName = "",
            IPostProcessor? process = null,
            string tag = "",
            PostProcessFailurePolicy failurePolicy = PostProcessFailurePolicy.Warning)
        {
            InitializeComponent();
            Title = title;
            _viewModel = new PostProcessMetaEditViewModel(
                templates,
                processes,
                metaName,
                templateName,
                process,
                tag,
                failurePolicy,
                string.IsNullOrWhiteSpace(metaName)
                    ? EngineLocalization.Get("新增后处理项")
                    : EngineLocalization.Get("编辑后处理项"));
            DataContext = _viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
            RefreshDraftConfigPanel();
        }

        private void ProcessType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshDraftConfigPanel();
        }

        private void RefreshDraftConfigPanel()
        {
            if (!IsInitialized || DraftProcessConfigPanel == null)
                return;

            DraftProcessConfigPanel.Children.Clear();
            object? config = SelectedProcess?.GetConfig();
            if (config == null)
            {
                DraftProcessConfigPanel.Children.Add(new TextBlock
                {
                    Text = SelectedProcess == null
                        ? EngineLocalization.Get("选择处理类型后显示配置")
                        : EngineLocalization.Get("此处理类型无需额外配置"),
                    Foreground = System.Windows.Media.Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0)
                });
                return;
            }

            DraftProcessConfigPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(config));
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MetaName))
            {
                MessageBox.Show(this, EngineLocalization.Get("名称不能为空"), "ColorVision");
                return;
            }
            if (SelectedTemplate == null)
            {
                MessageBox.Show(this, EngineLocalization.Get("请选择流程模板"), "ColorVision");
                return;
            }
            if (SelectedProcess == null)
            {
                MessageBox.Show(this, EngineLocalization.Get("请选择处理类型"), "ColorVision");
                return;
            }

            DialogResult = true;
        }
    }

    internal sealed class PostProcessMetaEditViewModel : ViewModelBase
    {
        private readonly IReadOnlyList<PostProcessTypeOption> _allOptions;
        private readonly List<TemplateModel<FlowParam>> _allTemplates;
        private IPostProcessor? _selectedProcessDraft;

        public ObservableCollection<TemplateModel<FlowParam>> VisibleTemplates { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();
        public ObservableCollection<PostProcessTypeOption> VisibleProcesses { get; } = new();
        public IReadOnlyList<PostProcessFailurePolicy> FailurePolicies { get; } =
            Enum.GetValues<PostProcessFailurePolicy>();

        public string PageTitle { get; }

        public string MetaName
        {
            get => _metaName;
            set
            {
                if (_metaName == value)
                    return;
                _metaName = value;
                OnPropertyChanged();
            }
        }
        private string _metaName;

        public string Tag
        {
            get => _tag;
            set
            {
                if (_tag == value)
                    return;
                _tag = value;
                OnPropertyChanged();
            }
        }
        private string _tag;

        public PostProcessFailurePolicy FailurePolicy
        {
            get => _failurePolicy;
            set
            {
                if (_failurePolicy == value)
                    return;
                _failurePolicy = value;
                OnPropertyChanged();
            }
        }
        private PostProcessFailurePolicy _failurePolicy;

        public TemplateModel<FlowParam>? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (_selectedTemplate == value)
                    return;
                _selectedTemplate = value;
                OnPropertyChanged();
            }
        }
        private TemplateModel<FlowParam>? _selectedTemplate;

        public string TemplateSearchText
        {
            get => _templateSearchText;
            set
            {
                if (_templateSearchText == value)
                    return;
                _templateSearchText = value;
                OnPropertyChanged();
                RefreshVisibleTemplates();
            }
        }
        private string _templateSearchText = string.Empty;

        public string? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory == value)
                    return;
                _selectedCategory = value;
                OnPropertyChanged();
                if (SelectedOption?.Category != value)
                    SelectedOption = null;
                RefreshVisibleProcesses();
            }
        }
        private string? _selectedCategory;

        public PostProcessTypeOption? SelectedOption
        {
            get => _selectedOption;
            set
            {
                if (_selectedOption == value)
                    return;
                _selectedOption = value;
                _selectedProcessDraft = value == null ? null : CreateProcessDraft(value.Process);
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedProcess));
            }
        }
        private PostProcessTypeOption? _selectedOption;

        public IPostProcessor? SelectedProcess => _selectedProcessDraft;

        public PostProcessMetaEditViewModel(
            IEnumerable<TemplateModel<FlowParam>> templates,
            IEnumerable<IPostProcessor> processes,
            string metaName,
            string templateName,
            IPostProcessor? process,
            string tag,
            PostProcessFailurePolicy failurePolicy,
            string pageTitle)
        {
            _allTemplates = templates.ToList();
            _allOptions = PostProcessTypeCatalog.CreateOptions(processes);
            _metaName = metaName;
            _tag = tag;
            _failurePolicy = failurePolicy;
            PageTitle = pageTitle;
            _selectedTemplate = _allTemplates.FirstOrDefault(template => template.Key == templateName)
                ?? (_allTemplates.Count > 0 ? _allTemplates[0] : null);
            _selectedOption = _allOptions.FirstOrDefault(option => option.FullTypeName == process?.GetType().FullName)
                ?? (_allOptions.Count > 0 ? _allOptions[0] : null);
            _selectedProcessDraft = _selectedOption == null
                ? null
                : CreateProcessDraft(process ?? _selectedOption.Process);

            foreach (string category in _allOptions.Select(option => option.Category).Distinct(StringComparer.Ordinal))
                Categories.Add(category);

            _selectedCategory = _selectedOption?.Category ?? (Categories.Count > 0 ? Categories[0] : null);
            RefreshVisibleTemplates();
            RefreshVisibleProcesses();
        }

        private static IPostProcessor CreateProcessDraft(IPostProcessor source)
        {
            IPostProcessor draft = source.CreateInstance();
            object? config = source.GetConfig();
            if (config != null)
                draft.SetConfig(JsonConvert.SerializeObject(config));
            return draft;
        }

        private void RefreshVisibleTemplates()
        {
            VisibleTemplates.Clear();
            string keyword = TemplateSearchText.Trim();
            foreach (TemplateModel<FlowParam> template in _allTemplates.Where(template =>
                         string.IsNullOrEmpty(keyword)
                         || template.Key.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)))
            {
                VisibleTemplates.Add(template);
            }

            OnPropertyChanged(nameof(SelectedTemplate));
        }

        private void RefreshVisibleProcesses()
        {
            VisibleProcesses.Clear();
            foreach (PostProcessTypeOption option in _allOptions.Where(option => option.Category == SelectedCategory))
                VisibleProcesses.Add(option);
        }
    }
}
