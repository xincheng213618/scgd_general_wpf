using ColorVision.Themes;
using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Globalization;

namespace ProjectARVRPro;

public partial class ProjectSettingsWindow : Window
{
    private readonly ProjectSettingsSession _session;
    private readonly Action _persist;
    private readonly Dictionary<ProjectSettingsPage, FrameworkElement> _contents = [];
    private bool _updatingNavigation;
    private ProjectSettingsPage? _anchorTarget;
    private readonly HashSet<(object Source, string Property)> _pendingChanges = [];
    private readonly Dictionary<(object Source, string Property), string> _saveErrors = [];
    private readonly Func<bool> _confirmDiscard;
    private DispatcherOperation? _saveOperation;
    private bool _suspendAutoSave = true;
    private string? _defaultsError;

    public ProjectSettingsWindow(ProjectARVRProConfig project, ViewResultManagerConfig results, bool sourceImageSupportsBmp, ProjectSettingsPage initialPage)
        : this(new ProjectSettingsSession(project, results, sourceImageSupportsBmp), initialPage, () => ConfigService.Instance.SaveConfigs()) { }

    internal ProjectSettingsWindow(ProjectSettingsSession session, ProjectSettingsPage initialPage, Action persist, Func<bool>? confirmDiscard = null)
    {
        _session = session;
        _persist = persist;
        _confirmDiscard = confirmDiscard ?? (() => MessageBox.Show(this,
            "部分修改未保存，仍要关闭吗？\n已自动保存的设置不会撤销。", "设置未保存",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
        InitializeComponent();
        this.ApplyCaption();
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 40);
        _session.Project.PropertyChanged += Draft_Changed;
        _session.Results.PropertyChanged += Draft_Changed;
        BuildSections();
        FilterSections();
        SettingsContent.AddHandler(Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>((_, _) => QueueAutoSave()));
        _suspendAutoSave = false;
        Loaded += (_, _) => ShowPage(initialPage);
        UpdateStatus();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (SettingsList != null && SettingsContent != null) FilterSections();
    }

    private void FilterSections()
    {
        FlushInputs(SettingsContent);
        var selected = SettingsList.SelectedItem as ProjectSettingsSection;
        var sections = _session.Sections.Where(section => section.Matches(SearchBox.Text.Trim())).ToArray();
        _updatingNavigation = true;
        foreach (var section in _session.Sections)
            _contents[section.Id].Visibility = sections.Contains(section) ? Visibility.Visible : Visibility.Collapsed;
        SettingsList.ItemsSource = sections;
        SettingsList.SelectedItem = sections.Contains(selected) ? selected : sections.FirstOrDefault();
        _updatingNavigation = false;
        EmptyState.Visibility = sections.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (SettingsList.SelectedItem is ProjectSettingsSection active) ScrollToSection(active);
    }

    private void SettingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingNavigation && SettingsList.SelectedItem is ProjectSettingsSection section) ScrollToSection(section);
    }

    private void SettingsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Clicking the active anchor should return to its heading too.
        if (ItemsControl.ContainerFromElement(SettingsList, e.OriginalSource as DependencyObject) is ListBoxItem item
            && item.Content is ProjectSettingsSection section) ScrollToSection(section);
    }

    private void ScrollToSection(ProjectSettingsSection section)
    {
        if (!_contents.TryGetValue(section.Id, out var content) || content.Visibility != Visibility.Visible) return;
        PageScroll.UpdateLayout();
        _anchorTarget = section.Id;
        PageScroll.ScrollToVerticalOffset(content.TranslatePoint(new Point(), SettingsContent).Y);
    }

    private void PageScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_updatingNavigation || e.OriginalSource != PageScroll
            || (e.VerticalChange == 0 && e.ExtentHeightChange == 0 && e.ViewportHeightChange == 0)) return;
        var sections = SettingsList.Items.Cast<ProjectSettingsSection>().ToArray();
        if (sections.Length == 0) return;
        ProjectSettingsSection? active = null;
        if (_anchorTarget is { } target && _contents.TryGetValue(target, out var targetContent))
        {
            double offset = Math.Min(targetContent.TranslatePoint(new Point(), SettingsContent).Y, PageScroll.ScrollableHeight);
            if (Math.Abs(PageScroll.VerticalOffset - offset) < 1) active = sections.FirstOrDefault(section => section.Id == target);
        }
        bool followsAnchor = active != null;
        _anchorTarget = null;
        active ??= PageScroll.ScrollableHeight > 0 && PageScroll.VerticalOffset >= PageScroll.ScrollableHeight - 1 ? sections.Last()
            : sections.LastOrDefault(section => _contents[section.Id].TranslatePoint(new Point(), SettingsContent).Y <= PageScroll.VerticalOffset + 1) ?? sections.First();
        // Once scrolling moves to another section, the old directory item must not retain a second highlight.
        // Keep keyboard navigation focus during anchor jumps, and never take focus away from an editor.
        if (!followsAnchor && !Equals(SettingsList.SelectedItem, active) && SettingsList.IsKeyboardFocusWithin) PageScroll.Focus();
        _updatingNavigation = true;
        SettingsList.SelectedItem = active;
        _updatingNavigation = false;
    }

    private void BuildSections()
    {
        _contents.Clear();
        SettingsContent.Children.Clear();
        foreach (var section in _session.Sections)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12), Tag = section.Id };
            var heading = new TextBlock { Text = section.Title, FontSize = 18, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, 0, 0, 10), ToolTip = section.Description };
            AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level1);
            panel.Children.Add(heading);
            if (section.Id == ProjectSettingsPage.Images)
                panel.Children.Add(new TextBlock { Text = "图像保存在“结果保存”指定的目录。", Margin = new Thickness(0, 0, 0, 10) });
            foreach (var group in section.Groups)
            {
                var normalProperties = group.Properties.Where(property => property.GetCustomAttribute<CategoryAttribute>()?.Category != "结果编号").ToArray();
                if (section.Id == ProjectSettingsPage.Images)
                {
                    foreach (var category in normalProperties.GroupBy(property => property.GetCustomAttribute<CategoryAttribute>()?.Category))
                    {
                        panel.Children.Add(new TextBlock { Text = category.Key,
                            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
                        panel.Children.Add(CreateSettingsGroup(group.Source, category));
                    }
                }
                else if (normalProperties.Length > 0) panel.Children.Add(CreateSettingsGroup(group.Source, normalProperties));
                var numbering = group.Properties.Where(property => property.GetCustomAttribute<CategoryAttribute>()?.Category == "结果编号").ToArray();
                if (numbering.Length > 0)
                {
                    panel.Children.Add(new TextBlock { Text = "结果编号规则", FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 6) });
                    panel.Children.Add(CreateSettingsGroup(group.Source, numbering));
                }
            }
            _contents.Add(section.Id, panel);
            SettingsContent.Children.Add(panel);
        }
    }

    private Border CreateSettingsGroup(object source, IEnumerable<PropertyInfo> properties)
    {
        var rows = new StackPanel();
        foreach (var property in properties)
        {
            // Reuse metadata editors, validation and conditional visibility; only the local layout is compact.
            var editor = PropertyEditorHelper.GenProperties(property, source);
            foreach (var textBox in TextInputs(editor))
            {
                var binding = PropertyEditorHelper.CreateTwoWayBinding(source, property, UpdateSourceTrigger.LostFocus);
                binding.UpdateSourceTrigger = UpdateSourceTrigger.LostFocus;
                binding.StringFormat = textBox.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.StringFormat;
                binding.ValidationRules.Add(new SettingValidationRule(source, property.Name));
                textBox.SetBinding(TextBox.TextProperty, binding);
                textBox.PreviewKeyDown -= PropertyEditorHelper.TextBox_PreviewKeyDown;
                textBox.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
                // Folder pickers update an unfocused text box. Typing waits for Enter or focus loss.
                textBox.TextChanged += (_, _) =>
                {
                    if (!_suspendAutoSave && textBox.IsLoaded && !textBox.IsKeyboardFocusWithin)
                        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                };
            }
            string title = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name;
            string? description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
            var label = editor.Children.OfType<TextBlock>().FirstOrDefault();
            if (label != null) editor.Children.Remove(label);
            editor.Margin = new Thickness(0);
            editor.VerticalAlignment = VerticalAlignment.Center;
            if (editor.Children.Count == 1 && editor.Children[0] is FrameworkElement control)
            {
                control.Margin = new Thickness(0);
                control.MinWidth = 0;
                AutomationProperties.SetName(control, title);
                AutomationProperties.SetHelpText(control, description ?? "");
                if (control is TextBox or ComboBox)
                {
                    control.Width = double.NaN;
                    control.MinHeight = 28;
                    control.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
                editor.LastChildFill = control is TextBox or ComboBox or Panel;
            }
            var row = new Grid { Margin = new Thickness(10, 5, 10, 5), MinHeight = 28, ToolTip = description };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            Grid.SetColumn(editor, 1);
            row.Children.Add(editor);
            row.SetBinding(VisibilityProperty, new Binding(nameof(Visibility)) { Source = editor });
            rows.Children.Add(row);
        }
        var card = new Border { Child = rows, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1),
            Padding = new Thickness(0, 3, 0, 3), Margin = new Thickness(0, 0, 0, 12) };
        card.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
        card.SetResourceReference(Border.BorderBrushProperty, "ButtonBorderBrush");
        return card;
    }

    private void Find_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
        e.Handled = true;
    }

    private void Search_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            SearchBox.Clear();
            e.Handled = true;
        }
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private sealed class SettingValidationRule(object source, string property) : ValidationRule(ValidationStep.ConvertedProposedValue, false)
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            ProjectSettingsSession.ValidateValue(source, property, value) is { } error ? new(false, error) : ValidationResult.ValidResult;
    }

    private void Draft_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendAutoSave || sender == null || string.IsNullOrEmpty(e.PropertyName)) return;
        _pendingChanges.Add((sender, e.PropertyName));
        QueueAutoSave();
    }

    private void QueueAutoSave()
    {
        if (_suspendAutoSave || _saveOperation?.Status == DispatcherOperationStatus.Pending) return;
        _saveOperation = Dispatcher.BeginInvoke(DispatcherPriority.DataBind, () => SavePendingChanges());
    }

    private void SavePendingChanges()
    {
        _saveOperation?.Abort();
        _saveOperation = null;
        foreach (var change in _pendingChanges.ToArray())
        {
            try
            {
                _session.SaveChange(change.Source, change.Property, _persist);
                _saveErrors.Remove(change);
            }
            catch (Exception ex) { _saveErrors[change] = $"自动保存失败，原设置未改变：{ex.Message}"; }
        }
        _pendingChanges.Clear();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusText.Text = _defaultsError ?? _saveErrors.Values.FirstOrDefault()
            ?? (FindValidationError(SettingsContent) != null ? "标红的输入未保存，请修正；其他设置已自动保存。" : "");
        StatusText.Visibility = string.IsNullOrEmpty(StatusText.Text) ? Visibility.Collapsed : Visibility.Visible;
        RetryButton.Visibility = _defaultsError != null || _saveErrors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (_defaultsError != null)
        {
            try { _session.Save(_persist); _defaultsError = null; _saveErrors.Clear(); }
            catch (Exception ex) { _defaultsError = $"恢复默认设置失败，原设置未改变：{ex.Message}"; }
            UpdateStatus();
            return;
        }
        foreach (var change in _saveErrors.Keys) _pendingChanges.Add(change);
        SavePendingChanges();
    }

    private void Defaults_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "将本窗口所有设置恢复为默认值并立即保存？", "恢复默认设置",
            MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;
        RestoreDefaults();
    }

    internal bool RestoreDefaults()
    {
        _suspendAutoSave = true;
        _saveOperation?.Abort();
        _pendingChanges.Clear();
        _saveErrors.Clear();
        _defaultsError = null;
        try
        {
            _session.RestoreDefaults();
            _session.Save(_persist);
            return true;
        }
        catch (Exception ex)
        {
            _defaultsError = $"恢复默认设置失败，原设置未改变：{ex.Message}";
            return false;
        }
        finally
        {
            BuildSections();
            FilterSections();
            _suspendAutoSave = false;
            UpdateStatus();
        }
    }

    private void ShowPage(ProjectSettingsPage page)
    {
        SearchBox.Clear();
        var section = _session.Sections.First(section => section.Id == page);
        _updatingNavigation = true;
        SettingsList.SelectedItem = section;
        _updatingNavigation = false;
        ScrollToSection(section);
    }

    private static void FlushInputs(DependencyObject? node)
    {
        if (node == null) return;
        if (node is TextBox textBox) textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) FlushInputs(child);
    }

    private static IEnumerable<TextBox> TextInputs(DependencyObject node)
    {
        if (node is TextBox textBox) yield return textBox;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            foreach (var input in TextInputs(child)) yield return input;
    }

    private static FrameworkElement? FindValidationError(DependencyObject node)
    {
        if (node is FrameworkElement element && Validation.GetHasError(node)) return element;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            if (FindValidationError(child) is { } invalid) return invalid;
        return null;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        FlushInputs(SettingsContent);
        SavePendingChanges();
        if ((FindValidationError(SettingsContent) != null || _saveErrors.Count > 0 || _defaultsError != null) && !_confirmDiscard()) e.Cancel = true;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _suspendAutoSave = true;
        _saveOperation?.Abort();
        _session.Project.PropertyChanged -= Draft_Changed;
        _session.Results.PropertyChanged -= Draft_Changed;
        base.OnClosed(e);
    }
}
