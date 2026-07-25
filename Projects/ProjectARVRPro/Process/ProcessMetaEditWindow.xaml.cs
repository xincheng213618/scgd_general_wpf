#pragma warning disable CA1852
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ProjectARVRPro.Process
{
    public partial class ProcessMetaEditWindow : Window
    {
        private readonly ProcessMetaEditViewModel _viewModel;

        public string MetaName => _viewModel.MetaName.Trim();
        public TemplateModel<FlowParam>? SelectedTemplate => _viewModel.SelectedTemplate;
        public IProcess? SelectedProcess => _viewModel.SelectedProcess;
        public bool IsMetaEnabled => _viewModel.IsEnabled;

        public ProcessMetaEditWindow(
            IEnumerable<TemplateModel<FlowParam>> templates,
            IEnumerable<IProcess> processes,
            string title,
            string metaName = "",
            string flowTemplate = "",
            IProcess? process = null,
            bool isEnabled = true,
            bool showMetaFields = true,
            bool isEdit = false,
            ProcessMetaEditTarget editTarget = ProcessMetaEditTarget.Choice)
        {
            InitializeComponent();
            Title = title;
            _viewModel = new ProcessMetaEditViewModel(
                templates,
                processes,
                metaName,
                flowTemplate,
                process,
                isEnabled,
                showMetaFields,
                isEdit,
                editTarget);
            DataContext = _viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsCreateBasicsPage && _viewModel.ShowMetaFields)
            {
                NameTextBox.Focus();
                NameTextBox.SelectAll();
            }

            RefreshDraftConfigPanel();
        }

        private void Primary_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsCreateBasicsPage)
            {
                if (!ValidateNameAndTemplate())
                    return;

                _viewModel.GoToProcessPage();
                RefreshDraftConfigPanel();
                return;
            }

            if (_viewModel.IsEditTemplatePage && !ValidateTemplate())
                return;

            if (_viewModel.IsProcessPage && SelectedProcess == null)
            {
                MessageBox.Show(this, "请选择处理类型", "ColorVision");
                return;
            }

            DialogResult = true;
        }

        private bool ValidateNameAndTemplate() => ValidateName() && ValidateTemplate();

        private bool ValidateName()
        {
            if (_viewModel.ShowMetaFields && string.IsNullOrWhiteSpace(MetaName))
            {
                MessageBox.Show(this, "名称不能为空", "ColorVision");
                return false;
            }

            return true;
        }

        private bool ValidateTemplate()
        {
            if (SelectedTemplate == null)
            {
                MessageBox.Show(this, "请选择流程模板", "ColorVision");
                return false;
            }

            return true;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.GoBack();
        }

        private void EditTemplate_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.BeginEditTemplate();
        }

        private void EditProcess_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.BeginEditProcess();
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
            object? config = SelectedProcess?.GetProcessConfig();
            if (config == null)
            {
                DraftProcessConfigPanel.Children.Add(new TextBlock
                {
                    Text = SelectedProcess == null ? "选择处理类型后显示配置" : "此处理类型无需额外配置",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0)
                });
                return;
            }

            DraftProcessConfigPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(config));
        }
    }

    public enum ProcessMetaEditTarget
    {
        Choice,
        Template,
        Process
    }

    internal enum ProcessMetaEditPage
    {
        CreateBasics,
        EditChoice,
        EditTemplate,
        Process
    }

    internal sealed class ProcessMetaEditViewModel : ViewModelBase
    {
        private readonly IReadOnlyList<ProcessTypeOption> _allOptions;
        private readonly List<TemplateModel<FlowParam>> _allTemplates;
        private readonly TemplateModel<FlowParam>? _originalTemplate;
        private readonly ProcessTypeOption? _originalOption;
        private readonly IProcess? _originalProcess;
        private readonly ProcessMetaEditTarget _editTarget;

        public ObservableCollection<TemplateModel<FlowParam>> VisibleTemplates { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();
        public ObservableCollection<string> Subcategories { get; } = new();
        public ObservableCollection<ProcessTypeOption> VisibleProcesses { get; } = new();

        public bool IsEditMode { get; }
        public bool ShowMetaFields { get; }
        public GridLength MetaColumnWidth => ShowMetaFields ? new GridLength(280) : new GridLength(0);

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

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                    return;

                _isEnabled = value;
                OnPropertyChanged();
            }
        }
        private bool _isEnabled;

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
                OnPropertyChanged(nameof(ShowSubcategories));

                if (SelectedOption?.Category != value)
                    SelectedOption = null;

                RefreshSubcategories();
                RefreshVisibleProcesses();
            }
        }
        private string? _selectedCategory;

        public string? SelectedSubcategory
        {
            get => _selectedSubcategory;
            set
            {
                if (_selectedSubcategory == value)
                    return;

                _selectedSubcategory = value;
                OnPropertyChanged();

                if (SelectedOption?.Subcategory != value)
                    SelectedOption = null;

                RefreshVisibleProcesses();
            }
        }
        private string? _selectedSubcategory;

        public bool ShowSubcategories => SelectedCategory == ProcessTypeCatalog.ArvrCategory;

        public ProcessTypeOption? SelectedOption
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
        private ProcessTypeOption? _selectedOption;
        private IProcess? _selectedProcessDraft;

        public IProcess? SelectedProcess => _selectedProcessDraft;

        public bool IsCreateBasicsPage => CurrentPage == ProcessMetaEditPage.CreateBasics;
        public bool IsEditChoicePage => CurrentPage == ProcessMetaEditPage.EditChoice;
        public bool IsEditTemplatePage => CurrentPage == ProcessMetaEditPage.EditTemplate;
        public bool IsProcessPage => CurrentPage == ProcessMetaEditPage.Process;
        public bool CanGoBack => !IsEditMode
            ? IsProcessPage
            : _editTarget == ProcessMetaEditTarget.Choice && (IsProcessPage || IsEditTemplatePage);
        public bool ShowPrimaryButton => !IsEditChoicePage;
        public string PrimaryButtonText => IsCreateBasicsPage ? "下一步" : "保存";
        public string StepText => !IsEditMode
            ? (IsCreateBasicsPage ? "步骤 1 / 2" : "步骤 2 / 2")
            : (IsEditChoicePage ? "选择修改项" : "单项编辑");

        public string PageTitle => CurrentPage switch
        {
            ProcessMetaEditPage.CreateBasics => ShowMetaFields ? "填写名称并选择流程模板" : "选择流程模板",
            ProcessMetaEditPage.EditChoice => "选择要修改的内容",
            ProcessMetaEditPage.EditTemplate => "修改流程模板",
            ProcessMetaEditPage.Process => "选择处理类型",
            _ => string.Empty
        };

        public string PageSubtitle => CurrentPage switch
        {
            ProcessMetaEditPage.CreateBasics => "完成基本信息后，再进入处理类型选择。",
            ProcessMetaEditPage.EditChoice => "每次只进入一个明确的编辑入口，类型再多也容易查找。",
            ProcessMetaEditPage.EditTemplate => "名称和处理类型保持不变。",
            ProcessMetaEditPage.Process => "先选业务分类；ARVR 可继续按测试项目筛选。",
            _ => string.Empty
        };

        private ProcessMetaEditPage CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage == value)
                    return;

                _currentPage = value;
                NotifyPageChanged();
            }
        }
        private ProcessMetaEditPage _currentPage;

        public ProcessMetaEditViewModel(
            IEnumerable<TemplateModel<FlowParam>> templates,
            IEnumerable<IProcess> processes,
            string metaName,
            string flowTemplate,
            IProcess? process,
            bool isEnabled,
            bool showMetaFields,
            bool isEdit,
            ProcessMetaEditTarget editTarget)
        {
            IsEditMode = isEdit;
            ShowMetaFields = showMetaFields;
            _editTarget = editTarget;
            _allTemplates = templates.ToList();
            _allOptions = ProcessTypeCatalog.CreateOptions(processes);

            _metaName = metaName;
            _isEnabled = isEnabled;
            _selectedTemplate = _allTemplates.FirstOrDefault(template => template.Key == flowTemplate)
                ?? (_allTemplates.Count > 0 ? _allTemplates[0] : null);
            _selectedOption = ProcessTypeCatalog.IsBlankProcess(process)
                ? null
                : _allOptions.FirstOrDefault(option => option.FullTypeName == process?.GetType().FullName);
            _selectedProcessDraft = _selectedOption == null
                ? null
                : CreateProcessDraft(process ?? _selectedOption.Process);

            _originalTemplate = _selectedTemplate;
            _originalOption = _selectedOption;
            _originalProcess = process;

            foreach (string category in new[]
                     {
                         ProcessTypeCatalog.ArvrCategory,
                         ProcessTypeCatalog.AoiCategory,
                         ProcessTypeCatalog.DemuraCategory
                     })
            {
                Categories.Add(category);
            }

            _selectedCategory = _selectedOption?.Category ?? ProcessTypeCatalog.ArvrCategory;
            RefreshVisibleTemplates();
            RefreshSubcategories(_selectedOption?.Subcategory);
            RefreshVisibleProcesses();

            _currentPage = IsEditMode
                ? editTarget switch
                {
                    ProcessMetaEditTarget.Template => ProcessMetaEditPage.EditTemplate,
                    ProcessMetaEditTarget.Process => ProcessMetaEditPage.Process,
                    _ => ProcessMetaEditPage.EditChoice
                }
                : ProcessMetaEditPage.CreateBasics;
        }

        public void GoToProcessPage()
        {
            CurrentPage = ProcessMetaEditPage.Process;
        }

        public void BeginEditTemplate()
        {
            SelectedTemplate = _originalTemplate;
            TemplateSearchText = string.Empty;
            CurrentPage = ProcessMetaEditPage.EditTemplate;
        }

        public void BeginEditProcess()
        {
            RestoreProcessSelection();
            CurrentPage = ProcessMetaEditPage.Process;
        }

        public void GoBack()
        {
            if (!IsEditMode && IsProcessPage)
            {
                CurrentPage = ProcessMetaEditPage.CreateBasics;
                return;
            }

            if (!IsEditMode)
                return;

            if (_editTarget != ProcessMetaEditTarget.Choice)
                return;

            if (IsEditTemplatePage)
            {
                SelectedTemplate = _originalTemplate;
                TemplateSearchText = string.Empty;
            }
            else if (IsProcessPage)
                RestoreProcessSelection();

            CurrentPage = ProcessMetaEditPage.EditChoice;
        }

        private void RestoreProcessSelection()
        {
            _selectedOption = _originalOption;
            _selectedProcessDraft = _originalOption == null
                ? null
                : CreateProcessDraft(_originalProcess ?? _originalOption.Process);
            _selectedCategory = _originalOption?.Category ?? ProcessTypeCatalog.ArvrCategory;
            OnPropertyChanged(nameof(SelectedOption));
            OnPropertyChanged(nameof(SelectedProcess));
            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(ShowSubcategories));
            RefreshSubcategories(_originalOption?.Subcategory);
            RefreshVisibleProcesses();
        }

        private static IProcess CreateProcessDraft(IProcess source)
        {
            IProcess draft = source.CreateInstance();
            object? config = source.GetProcessConfig();
            if (config != null)
            {
                draft.SetProcessConfig(JsonConvert.SerializeObject(config));
            }

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

        private void RefreshSubcategories(string? preferredSubcategory = null)
        {
            Subcategories.Clear();
            if (!ShowSubcategories)
            {
                _selectedSubcategory = null;
                OnPropertyChanged(nameof(SelectedSubcategory));
                return;
            }

            foreach (string subcategory in _allOptions
                         .Where(option => option.Category == ProcessTypeCatalog.ArvrCategory)
                         .Select(option => option.Subcategory)
                         .Distinct(StringComparer.Ordinal))
            {
                Subcategories.Add(subcategory);
            }

            string? nextSubcategory = preferredSubcategory;
            if (string.IsNullOrEmpty(nextSubcategory) || !Subcategories.Contains(nextSubcategory))
            {
                nextSubcategory = _selectedSubcategory != null && Subcategories.Contains(_selectedSubcategory)
                    ? _selectedSubcategory
                    : (Subcategories.Count > 0 ? Subcategories[0] : null);
            }

            _selectedSubcategory = nextSubcategory;
            OnPropertyChanged(nameof(SelectedSubcategory));
        }

        private void RefreshVisibleProcesses()
        {
            VisibleProcesses.Clear();
            foreach (ProcessTypeOption option in _allOptions.Where(option =>
                         option.Category == SelectedCategory
                         && (!ShowSubcategories || option.Subcategory == SelectedSubcategory)))
            {
                VisibleProcesses.Add(option);
            }
        }

        private void NotifyPageChanged()
        {
            OnPropertyChanged(nameof(IsCreateBasicsPage));
            OnPropertyChanged(nameof(IsEditChoicePage));
            OnPropertyChanged(nameof(IsEditTemplatePage));
            OnPropertyChanged(nameof(IsProcessPage));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(ShowPrimaryButton));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(StepText));
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(PageSubtitle));
        }
    }
}
