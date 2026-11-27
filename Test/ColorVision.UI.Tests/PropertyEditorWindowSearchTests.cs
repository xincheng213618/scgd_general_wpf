using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class PropertyEditorWindowSearchTests
{
    [Fact]
    public void SearchPreservesEditorAndUpdatesVisibilityWhenMetadataChanges()
    {
        WpfTestHost.Invoke(() =>
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var theme = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
                .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
            foreach (var dictionary in theme)
                dictionaries.Add(dictionary);
            CultureInfo previousCulture = CultureInfo.CurrentUICulture;
            PropertyEditorWindow? window = null;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                var config = new SearchConfig();
                PropertyEditorHelper.GetResourceManager(config, new SearchResources());
                ChangingLabelAttribute.Label = "initial-dynamic-label";
                window = new PropertyEditorWindow(config) { Left = -5000, ShowActivated = false, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();

                var search = (TextBox)window.FindName("SearchBox");
                var panel = (StackPanel)window.FindName("PropertyPanel");
                DockPanel FindRow(string name) => panel.Children.OfType<Border>()
                    .SelectMany(border => ((StackPanel)border.Child).Children.OfType<DockPanel>())
                    .Single(row => row.Tag is PropertyInfo property && property.Name == name);
                DockPanel row = FindRow(nameof(SearchConfig.Value));
                DockPanel dynamicRow = FindRow(nameof(SearchConfig.Dynamic));
                var editor = Assert.Single(row.Children.OfType<TextBox>());

                search.Text = "english-label";
                Assert.True(row.IsVisible);
                search.Text = "distinct-hint";
                Assert.True(row.IsVisible);
                config.Enabled = false;
                Assert.False(row.IsVisible);
                config.Enabled = true;
                Assert.True(row.IsVisible);

                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                search.Text = "chinese-label";
                Assert.True(row.IsVisible);
                search.Text = "english-label";
                Assert.False(row.IsVisible);

                search.Text = "initial-dynamic-label";
                Assert.True(dynamicRow.IsVisible);
                ChangingLabelAttribute.Label = "updated-dynamic-label";
                search.Text = "updated-dynamic-label";
                Assert.True(dynamicRow.IsVisible);
                search.Text = "initial-dynamic-label";
                Assert.False(dynamicRow.IsVisible);

                search.Text = string.Empty;
                Assert.Same(row, FindRow(nameof(SearchConfig.Value)));
                Assert.Same(editor, Assert.Single(row.Children.OfType<TextBox>()));
                editor.Text = "edited";
                editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Equal("edited", config.Value);
            }
            finally
            {
                window?.Close();
                PropertyEditorHelper.ResourceManagerCache.TryRemove(typeof(SearchConfig), out _);
                CultureInfo.CurrentUICulture = previousCulture;
                foreach (var dictionary in theme)
                    dictionaries.Remove(dictionary);
            }
        });
    }

    private sealed class SearchConfig : ViewModelBase
    {
        private bool enabled = true;
        public bool Enabled { get => enabled; set { enabled = value; OnPropertyChanged(); } }

        [Category("Search")]
        [DisplayName("LabelKey")]
        [Description("distinct-hint")]
        [PropertyVisibility(nameof(Enabled))]
        public string Value { get; set; } = "original";

        [ChangingLabel]
        public string Dynamic { get; set; } = string.Empty;
    }

    private sealed class SearchResources : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture) => name == "LabelKey"
            ? culture?.Name == "zh-CN" ? "chinese-label" : "english-label"
            : null;
    }

    private sealed class ChangingLabelAttribute : DisplayNameAttribute
    {
        internal static string Label { get; set; } = string.Empty;

        public override string DisplayName => Label;
    }
}
