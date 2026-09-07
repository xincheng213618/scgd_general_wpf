using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using EditorResources = ColorVision.ImageEditor.Properties.Resources;

namespace ColorVision.ImageEditor.Settings
{
    public partial class ImageViewSettingsWindow : Window
    {
        private readonly ImageView _imageView;
        private readonly List<SettingsPage> _pages;
        private readonly ImageSettingsSession _session;
        private bool _skipSave;
        private bool _contextChanged;
        private readonly string? _profileIdentity;

        public ImageViewSettingsWindow(ImageView imageView, string? initialGroup = null)
        {
            _imageView = imageView ?? throw new ArgumentNullException(nameof(imageView));
            ObjectDisposedException.ThrowIf(imageView.EditorContext.ProcessingContext.IsDisposed, imageView);
            _profileIdentity = imageView.Config.CalibrationProfileKey;
            var entries = ImageSettingsCatalog.Create(imageView);
            _session = new ImageSettingsSession(entries);
            _pages = entries.GroupBy(entry => entry.PageId).Select(group => new SettingsPage(group.Key, group.ToArray())).OrderBy(page => page.SectionOrder).ToList();
            InitializeComponent();
            _session.Changed += Session_Changed;
            _imageView.Config.Cleared += Context_Cleared;
            _imageView.ImageSourceLoaded += ImageSource_Loaded;
            FilterPages();
            string? initialId = ResolveInitialGroup(initialGroup);
            SettingsList.SelectedItem = _pages.FirstOrDefault(page => page.Id == initialId || page.Header == initialGroup)
                ?? _pages.FirstOrDefault(page => page.Id == ImageSettingsCategories.Display);
            UpdateStatus();
        }

        private static string? ResolveInitialGroup(string? group)
        {
            if (group == EditorResources.Settings_GroupDefaults) return ImageSettingsCategories.Defaults;
            if (group == EditorResources.Settings_GroupDisplay || group == EditorResources.Settings_GroupWorkspace) return ImageSettingsCategories.Display;
            if (group == EditorResources.Settings_GroupContext) return ImageSettingsCategories.Information;
            if (group == EditorResources.Settings_GroupLoader) return ImageSettingsCategories.FileOpening;
            if (group == "Shader Filter") return ImageSettingsCategories.Filters;
            if (group == EditorResources.PseudoColor_Group) return ImageSettingsCategories.PseudoColor;
            return group;
        }

        private void Search_Changed(object sender, TextChangedEventArgs e)
        {
            if (SettingsList != null) FilterPages();
        }

        private void FilterPages()
        {
            string search = SearchBox.Text.Trim();
            SettingsPage? selected = SettingsList.SelectedItem as SettingsPage;
            var filtered = _pages.Where(page => page.Matches(search)).ToList();
            ListCollectionView collection = new(filtered);
            collection.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SettingsPage.Section)));
            SettingsList.ItemsSource = collection;
            SettingsList.SelectedItem = selected != null && filtered.Contains(selected) ? selected : filtered.FirstOrDefault();
            if (filtered.Count == 0) { PageTitle.Text = SettingsText.Empty; SettingsContent.Content = null; }
        }

        private void SettingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsList.SelectedItem is not SettingsPage page) return;
            PageTitle.Text = page.Header;
            page.Content ??= CreatePage(page);
            SettingsContent.Content = page.Content;
            PageScroll.ScrollToTop();
        }

        private FrameworkElement CreatePage(SettingsPage page)
        {
            StackPanel panel = new();
            foreach (ImageViewSettingsEntry entry in page.Entries)
            {
                StackPanel section = new() { Margin = new Thickness(0, 0, 0, 20) };
                if (page.Entries.Length > 1 || entry.Title != page.Header)
                    section.Children.Add(new TextBlock { Text = entry.Title, FontWeight = FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap });
                string scope = ScopeName(entry.Scope);
                TextBlock caption = new() { Text = entry.OwnerId == "ImageEditor" ? scope : $"{scope} · {SettingsText.Provider}：{entry.OwnerId}", FontSize = 11, Margin = new Thickness(0, 4, 0, 6) };
                caption.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
                section.Children.Add(caption);
                string description = entry.Description;
                if (entry.Scope == ImageSettingsScope.Extension && string.IsNullOrEmpty(description)) description = SettingsText.ExternalHint;
                if (!string.IsNullOrEmpty(description))
                {
                    TextBlock hint = new() { Text = description, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
                    hint.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
                    section.Children.Add(hint);
                }
                FrameworkElement content;
                try { content = entry.CreateView?.Invoke() ?? SettingsPropertyPresenter.Create(entry.Source, entry.PropertyNames); }
                catch (Exception ex) { content = new TextBlock { Text = $"{SettingsText.Unavailable} {ex.Message}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(14) }; }
                if (entry.IsReadOnly && entry.CreateView == null) content.IsEnabled = false;
                Border card = new() { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Child = content };
                card.SetResourceReference(Border.BorderBrushProperty, "ButtonBorderBrush");
                card.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
                section.Children.Add(card);
                WrapPanel actions = new() { Margin = new Thickness(0, 10, 0, 0) };
                foreach (ImageSettingsAction action in entry.Actions) AddAction(actions, action);
                if (entry.Scope == ImageSettingsScope.Extension && entry.Save != null && !entry.IsReadOnly)
                    AddAction(actions, new ImageSettingsAction(SettingsText.Save, entry.Save));
                if (actions.Children.Count > 0) section.Children.Add(actions);
                if (_contextChanged && IsContextBound(entry)) section.IsEnabled = false;
                section.Tag = entry;
                panel.Children.Add(section);
            }
            return panel;
        }

        private void AddAction(Panel panel, ImageSettingsAction action)
        {
            Button button = new() { Content = action.Title, Margin = new Thickness(0, 0, 8, 4), Padding = new Thickness(12, 5, 12, 5) };
            button.SetResourceReference(StyleProperty, "ButtonDefault");
            button.Click += (_, _) =>
            {
                if (!ValidateInputs()) return;
                try
                {
                    action.Execute();
                    _session.AcceptSaved(action.SavedSource);
                    StatusText.Text = _session.HasChanges ? SettingsText.Pending : SettingsText.Applied;
                }
                catch (Exception ex) { ShowError(ex.Message); }
            };
            panel.Children.Add(button);
        }

        private bool IsContextBound(ImageViewSettingsEntry entry) => entry.Scope is ImageSettingsScope.CurrentImage or ImageSettingsScope.SourceProfile or ImageSettingsScope.Extension
            || (_imageView.EditorContext.ProcessingContext.IsDisposed && entry.Scope == ImageSettingsScope.CurrentView);

        private void ImageSource_Loaded(object? sender, ImageViewImageSourceLoadedEventArgs e)
        {
            if (_profileIdentity != _imageView.Config.CalibrationProfileKey) Context_Cleared(sender, EventArgs.Empty);
        }
        private void Context_Cleared(object? sender, EventArgs e)
        {
            _contextChanged = true;
            foreach (var section in _pages.Select(page => page.Content).OfType<Panel>().SelectMany(panel => panel.Children.OfType<FrameworkElement>()))
                if (section.Tag is ImageViewSettingsEntry entry && IsContextBound(entry)) section.IsEnabled = false;
            StatusText.Text = SettingsText.ConfigChanged;
        }

        private void Session_Changed(object? sender, EventArgs e) => UpdateStatus();
        private void UpdateStatus()
        {
            SaveButton.IsEnabled = _session.HasChanges;
            StatusText.Text = _contextChanged ? SettingsText.ConfigChanged : _session.HasChanges ? SettingsText.Pending : SettingsText.Ready;
        }

        private bool ValidateInputs()
        {
            if (Keyboard.FocusedElement is TextBox textBox) textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            foreach (var page in _pages)
                if (page.Content != null && HasValidationError(page.Content))
                {
                    SettingsList.SelectedItem = page;
                    StatusText.Text = SettingsText.ValidationError;
                    return false;
                }
            return true;
        }

        private static bool HasValidationError(DependencyObject node)
        {
            if (Validation.GetHasError(node)) return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                if (HasValidationError(VisualTreeHelper.GetChild(node, i))) return true;
            return false;
        }

        private bool SaveChanges()
        {
            if (!ValidateInputs()) return false;
            var errors = _session.SaveChanged();
            if (errors.Count > 0) { ShowError(string.Join(Environment.NewLine, errors)); return false; }
            SkipSaveButton.Visibility = Visibility.Collapsed;
            StatusText.Text = SettingsText.Saved;
            return true;
        }

        private void ShowError(string message)
        {
            StatusText.Text = $"{SettingsText.SaveFailed} {message}";
            SkipSaveButton.Visibility = Visibility.Visible;
        }
        private void Save_Click(object sender, RoutedEventArgs e) => SaveChanges();
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void SkipSave_Click(object sender, RoutedEventArgs e) { _skipSave = true; Close(); }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_skipSave && !SaveChanges()) e.Cancel = true;
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _imageView.Config.Cleared -= Context_Cleared;
            _imageView.ImageSourceLoaded -= ImageSource_Loaded;
            _session.Changed -= Session_Changed;
            _session.Dispose();
            base.OnClosed(e);
        }

        private static string ScopeName(ImageSettingsScope scope) => scope switch
        {
            ImageSettingsScope.CurrentView => SettingsText.CurrentView,
            ImageSettingsScope.CurrentImage => SettingsText.CurrentImage,
            ImageSettingsScope.Application => SettingsText.Application,
            ImageSettingsScope.Defaults => SettingsText.Defaults,
            ImageSettingsScope.SourceProfile => SettingsText.SourceProfile,
            _ => SettingsText.Extension
        };

        private sealed class SettingsPage(string id, ImageViewSettingsEntry[] entries)
        {
            public string Id { get; } = id;
            public string Header => entries[0].Group;
            public int SectionOrder => entries[0].Scope switch
            {
                ImageSettingsScope.CurrentView or ImageSettingsScope.SourceProfile => 0,
                ImageSettingsScope.Application or ImageSettingsScope.Defaults => 1,
                ImageSettingsScope.CurrentImage => 2,
                _ => 3
            };
            public string Section => entries[0].Scope switch
            {
                ImageSettingsScope.CurrentView or ImageSettingsScope.SourceProfile => SettingsText.CurrentView,
                ImageSettingsScope.Application or ImageSettingsScope.Defaults => SettingsText.Application,
                ImageSettingsScope.CurrentImage => SettingsText.CurrentImage,
                _ => SettingsText.Extension
            };
            public ImageViewSettingsEntry[] Entries => entries;
            public FrameworkElement? Content { get; set; }
            public bool Matches(string query) => string.IsNullOrEmpty(query) || entries.Any(entry =>
                $"{entry.Group} {entry.Title} {entry.Description} {entry.OwnerId}".Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || (!entry.IsReadOnly && entry.CreateView == null && PropertyEditorHelper.GetEditableProperties(entry.Source.GetType())
                    .Where(property => entry.PropertyNames == null || entry.PropertyNames.Contains(property.Name)).Any(property =>
                    (property.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? PropertyEditorHelper.GetDisplayName(PropertyEditorHelper.GetResourceManager(entry.Source), property)).Contains(query, StringComparison.CurrentCultureIgnoreCase))));
        }
    }
}
