using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates
{
    public enum TemplateCreateSourceKind
    {
        Default,
        Prepared,
        Existing,
        Sample,
        File
    }

    public sealed class TemplateCreateOptions
    {
        public TemplateCreateSourceKind? InitialSourceKind { get; set; }
        public int InitialTemplateIndex { get; set; } = -1;
        public string? SuggestedName { get; set; }
        public bool IsSourceSelectionVisible { get; set; } = true;
    }

    public sealed class TemplateCreatedEventArgs : EventArgs
    {
        public TemplateCreatedEventArgs(string templateName)
        {
            TemplateName = templateName;
        }

        public string TemplateName { get; }
    }

    public partial class TemplateCreateView : UserControl
    {
        private readonly List<TemplateCreateSource> _templateSources = new();
        private ITemplate? _template;
        private TemplateCreateSource? _selectedSource;
        private TemplateCreateSource? _appliedSource;
        private bool _isRefreshingSources;
        private bool _isSettingName;
        private bool _nameWasEdited;

        public TemplateCreateView()
        {
            InitializeComponent();
        }

        public event EventHandler<TemplateCreatedEventArgs>? TemplateCreated;
        public event EventHandler? CancelRequested;

        public void Initialize(ITemplate template, TemplateCreateOptions? options = null)
        {
            _template = template;
            _selectedSource = null;
            _appliedSource = null;
            _nameWasEdited = false;
            ValidationTextBlock.Visibility = Visibility.Collapsed;
            _isRefreshingSources = true;
            SourceSearchTextBox.Text = string.Empty;
            _isRefreshingSources = false;

            BuildTemplateSources();
            bool isSourceSelectionVisible = options?.IsSourceSelectionVisible ?? true;
            SourcePanel.Visibility = isSourceSelectionVisible ? Visibility.Visible : Visibility.Collapsed;
            SourceSearchTextBox.Visibility = isSourceSelectionVisible && _templateSources.Count > 6 ? Visibility.Visible : Visibility.Collapsed;
            TemplateCreateSource? initialSource = FindInitialSource(options);
            RenderTemplateSources(initialSource);

            string suggestedName = options?.SuggestedName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(suggestedName) && initialSource?.Kind == TemplateCreateSourceKind.Prepared)
                suggestedName = template.ImportName;
            if (string.IsNullOrWhiteSpace(suggestedName))
                suggestedName = template.NewCreateFileName(template.Code);

            SetSuggestedName(suggestedName);
            Dispatcher.BeginInvoke(() =>
            {
                CreateNameTextBox.Focus();
                CreateNameTextBox.SelectAll();
            });
        }

        public void Discard()
        {
            _template?.ClearCreateTemplateSource();
            _selectedSource = null;
            _appliedSource = null;
            PropertyGrid1.SelectedObject = null;
            CustomPreviewHost.Children.Clear();
        }

        private void BuildTemplateSources()
        {
            _templateSources.Clear();
            if (_template == null)
                return;

            if (_template.HasCreateTemplateSource)
            {
                _templateSources.Add(new TemplateCreateSource
                {
                    Kind = TemplateCreateSourceKind.Prepared,
                    Title = string.IsNullOrWhiteSpace(_template.ImportName) ? "已准备来源" : _template.ImportName,
                    Description = "使用刚复制或导入的模板内容",
                    SourceLabel = "当前"
                });
            }

            _templateSources.Add(new TemplateCreateSource
            {
                Kind = TemplateCreateSourceKind.Default,
                Title = Properties.Resources.DefaultTemplate,
                Description = Properties.Resources.UseSystemDefaultTemplate,
                SourceLabel = "默认"
            });

            bool supportsCopy = _template.GetType().GetMethod(nameof(ITemplate.CopyTo), [typeof(int)])?.DeclaringType != typeof(ITemplate);
            if (supportsCopy)
            {
                for (int index = 0; index < _template.Count; index++)
                {
                    string templateName;
                    try
                    {
                        templateName = _template.GetTemplateName(index);
                    }
                    catch
                    {
                        continue;
                    }

                    _templateSources.Add(new TemplateCreateSource
                    {
                        Kind = TemplateCreateSourceKind.Existing,
                        TemplateIndex = index,
                        Title = templateName,
                        Description = "从现有模板创建独立副本",
                        SourceLabel = "现有"
                    });
                }
            }

            bool supportsSample = _template.GetType().GetMethod(nameof(ITemplate.ImportJsonContent), [typeof(string), typeof(string)])?.DeclaringType != typeof(ITemplate)
                || _template.GetType().GetMethod(nameof(ITemplate.ImportFile), [typeof(string)])?.DeclaringType != typeof(ITemplate);
            if (supportsSample)
            {
                foreach (TemplateSampleRecord sample in TemplateSampleLibrary.GetInstance().GetSamples(_template))
                {
                    _templateSources.Add(new TemplateCreateSource
                    {
                        Kind = TemplateCreateSourceKind.Sample,
                        Title = sample.Name,
                        Description = string.IsNullOrWhiteSpace(sample.Description)
                            ? $"{sample.GroupName} · {sample.UpdatedAt:yyyy-MM-dd HH:mm}"
                            : sample.Description,
                        SourceLabel = "样例",
                        Sample = sample
                    });
                }
            }

            bool supportsImport = _template.GetType().GetMethod(nameof(ITemplate.Import), Type.EmptyTypes)?.DeclaringType != typeof(ITemplate);
            if (supportsImport)
            {
                _templateSources.Add(new TemplateCreateSource
                {
                    Kind = TemplateCreateSourceKind.File,
                    Title = "导入文件",
                    Description = "从 JSON 或模板文件创建",
                    SourceLabel = "文件"
                });
            }
        }

        private TemplateCreateSource? FindInitialSource(TemplateCreateOptions? options)
        {
            if (options?.InitialSourceKind == TemplateCreateSourceKind.Existing && options.InitialTemplateIndex >= 0)
            {
                TemplateCreateSource? existingSource = _templateSources.FirstOrDefault(source => source.Kind == TemplateCreateSourceKind.Existing && source.TemplateIndex == options.InitialTemplateIndex);
                if (existingSource != null)
                    return existingSource;
            }

            if (options?.InitialSourceKind != null)
            {
                TemplateCreateSource? requestedSource = _templateSources.FirstOrDefault(source => source.Kind == options.InitialSourceKind);
                if (requestedSource != null)
                    return requestedSource;
            }

            return _templateSources.FirstOrDefault(source => source.Kind == TemplateCreateSourceKind.Prepared)
                ?? _templateSources.FirstOrDefault(source => source.Kind == TemplateCreateSourceKind.Default)
                ?? _templateSources.FirstOrDefault();
        }

        private void RenderTemplateSources(TemplateCreateSource? preferredSource = null)
        {
            string keyword = SourceSearchTextBox.Text.Trim();
            List<TemplateCreateSource> visibleSources = _templateSources
                .Where(source => string.IsNullOrWhiteSpace(keyword)
                    || source.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || source.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || source.SourceLabel.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

            TemplateCreateSource? sourceToSelect = preferredSource != null && visibleSources.Contains(preferredSource)
                ? preferredSource
                : _selectedSource != null
                    ? visibleSources.Contains(_selectedSource) ? _selectedSource : null
                    : visibleSources.FirstOrDefault();

            _isRefreshingSources = true;
            SourceListBox.ItemsSource = visibleSources;
            SourceListBox.SelectedItem = sourceToSelect;
            _isRefreshingSources = false;

            if (sourceToSelect != null && !ReferenceEquals(sourceToSelect, _selectedSource))
                SelectTemplateSource(sourceToSelect);
        }

        private void SelectTemplateSource(TemplateCreateSource source)
        {
            TemplateCreateSource? previousSource = _selectedSource;
            if (!ApplyTemplateSource(source))
            {
                _isRefreshingSources = true;
                SourceListBox.SelectedItem = previousSource;
                _isRefreshingSources = false;
                return;
            }

            _selectedSource = source;
            UpdateSuggestedNameForSource(source);

            if (source.Kind != TemplateCreateSourceKind.Prepared
                && _templateSources.RemoveAll(item => item.Kind == TemplateCreateSourceKind.Prepared) > 0)
            {
                RenderTemplateSources(source);
            }
        }

        private bool ApplyTemplateSource(TemplateCreateSource source)
        {
            if (_template == null)
                return false;
            if (ReferenceEquals(source, _appliedSource))
                return true;

            bool applied;
            try
            {
                switch (source.Kind)
                {
                    case TemplateCreateSourceKind.Prepared:
                        applied = _template.HasCreateTemplateSource;
                        break;
                    case TemplateCreateSourceKind.Default:
                        _template.ClearCreateTemplateSource();
                        applied = true;
                        break;
                    case TemplateCreateSourceKind.Existing:
                        applied = source.TemplateIndex >= 0 && _template.CopyTo(source.TemplateIndex);
                        break;
                    case TemplateCreateSourceKind.Sample:
                        applied = source.Sample != null && _template.ImportJsonContent(source.Sample.Name, source.Sample.Content);
                        break;
                    case TemplateCreateSourceKind.File:
                        applied = _template.Import();
                        if (applied)
                        {
                            source.Title = string.IsNullOrWhiteSpace(_template.ImportName) ? "已导入文件" : _template.ImportName;
                            source.Description = "使用已选择的模板文件创建";
                            SourceListBox.Items.Refresh();
                        }
                        break;
                    default:
                        applied = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                ShowValidation($"模板来源加载失败：{ex.Message}");
                _appliedSource = null;
                return false;
            }

            if (!applied)
            {
                if (source.Kind != TemplateCreateSourceKind.File)
                    ShowValidation("模板来源加载失败，请重新选择。");
                _appliedSource = null;
                return false;
            }

            _appliedSource = source;
            ValidationTextBlock.Visibility = Visibility.Collapsed;
            UpdateCreatePreview();
            return true;
        }

        private void UpdateCreatePreview()
        {
            if (_template == null || _template.IsSideHide)
            {
                SourceColumn.Width = new GridLength(1, GridUnitType.Star);
                PropertyColumn.Width = new GridLength(0);
                PropertyPreviewBorder.Visibility = Visibility.Collapsed;
                return;
            }

            SourceColumn.Width = new GridLength(372);
            PropertyColumn.Width = new GridLength(1, GridUnitType.Star);
            PropertyPreviewBorder.Visibility = Visibility.Visible;
            try
            {
                object createParam = _template.CreateDefault();
                if (_template.IsUserControl)
                {
                    PropertyGridHost.Visibility = Visibility.Collapsed;
                    CustomPreviewHost.Visibility = Visibility.Visible;
                    CustomPreviewHost.Children.Clear();

                    UserControl userControl = _template.CreateUserControl();
                    if (userControl.Parent is Panel parent)
                        parent.Children.Remove(userControl);
                    userControl.Height = double.NaN;
                    userControl.Width = double.NaN;
                    CustomPreviewHost.Children.Add(userControl);
                    if (userControl is ITemplateUserControl templateUserControl)
                        templateUserControl.SetParam(createParam);
                }
                else
                {
                    CustomPreviewHost.Children.Clear();
                    CustomPreviewHost.Visibility = Visibility.Collapsed;
                    PropertyGridHost.Visibility = Visibility.Visible;
                    PropertyGrid1.SelectedObject = createParam;
                }
            }
            catch (Exception ex)
            {
                SourceColumn.Width = new GridLength(1, GridUnitType.Star);
                PropertyColumn.Width = new GridLength(0);
                PropertyPreviewBorder.Visibility = Visibility.Collapsed;
                ShowValidation($"模板预览加载失败：{ex.Message}");
            }
        }

        private void UpdateSuggestedNameForSource(TemplateCreateSource source)
        {
            if (_template == null || _nameWasEdited)
                return;

            string suggestedName = source.Kind switch
            {
                TemplateCreateSourceKind.Prepared when !string.IsNullOrWhiteSpace(_template.ImportName) => _template.ImportName,
                TemplateCreateSourceKind.Existing => _template.NewCreateFileName($"{source.Title}_Copy"),
                TemplateCreateSourceKind.Sample => _template.NewCreateFileName(source.Title),
                TemplateCreateSourceKind.File when !string.IsNullOrWhiteSpace(_template.ImportName) => _template.ImportName,
                _ => _template.NewCreateFileName(_template.Code)
            };
            SetSuggestedName(suggestedName);
        }

        private void SetSuggestedName(string name)
        {
            _isSettingName = true;
            CreateNameTextBox.Text = name;
            _isSettingName = false;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_template == null)
                return;

            string templateName = CreateNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(templateName))
            {
                ShowValidation(Properties.Resources.InputTemplateName);
                CreateNameTextBox.Focus();
                return;
            }

            if (_template.ExitsTemplateName(templateName))
            {
                var duplicateTemplate = TemplateControl.FindDuplicateTemplate(templateName);
                ShowValidation($"{duplicateTemplate?.GetType().Name} {Properties.Resources.AlreadyExists} {templateName}{Properties.Resources.Template}");
                CreateNameTextBox.Focus();
                return;
            }

            if (_selectedSource == null)
            {
                ShowValidation("请选择创建来源。");
                return;
            }

            if (!ReferenceEquals(_selectedSource, _appliedSource) && !ApplyTemplateSource(_selectedSource))
                return;

            if (!_template.TryCreateTemplate(templateName, out string message))
            {
                ShowValidation(message);
                return;
            }

            _template.ClearCreateTemplateSource();
            TemplateCreated?.Invoke(this, new TemplateCreatedEventArgs(templateName));
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Discard();
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SourceListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshingSources || SourceListBox.SelectedItem is not TemplateCreateSource source)
                return;
            SelectTemplateSource(source);
        }

        private void SourceSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_template == null || _isRefreshingSources)
                return;
            RenderTemplateSources();
        }

        private void TemplateCreateView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            Discard();
            CancelRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void CreateNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isSettingName)
                _nameWasEdited = true;
            ValidationTextBlock.Visibility = Visibility.Collapsed;
        }

        private void CreateNameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            CreateButton_Click(sender, e);
            e.Handled = true;
        }

        private void ShowValidation(string message)
        {
            ValidationTextBlock.Text = message;
            ValidationTextBlock.Visibility = Visibility.Visible;
        }

        private sealed class TemplateCreateSource
        {
            public TemplateCreateSourceKind Kind { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string SourceLabel { get; set; } = string.Empty;
            public int TemplateIndex { get; set; } = -1;
            public TemplateSampleRecord? Sample { get; set; }
        }
    }
}
