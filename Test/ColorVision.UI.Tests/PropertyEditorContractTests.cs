using ColorVision.UI.LogImp;
using ColorVision.ImageEditor.Algorithms.Mtf;
using log4net.Core;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public class PropertyEditorContractTests
{
    private sealed class DisplayMetadataConfig
    {
        [Display(Name = "Visible name", Description = "Visible description", GroupName = "Visible group")]
        public string Value { get; set; } = "Value";

        [DisplayName("Legacy name"), Description("Legacy description")]
        [Display(Name = "New name", Description = "New description")]
        public string Legacy { get; set; } = "Legacy";

        [Display(Name = "MissingResource", ResourceType = typeof(DisplayMetadataConfig))]
        public string InvalidResource { get; set; } = "Still editable";
    }

    [Fact]
    public void DisplayMetadata_ProvidesLabelsAndDescriptionsWithoutOverridingLegacyAttributes()
    {
        var value = typeof(DisplayMetadataConfig).GetProperty(nameof(DisplayMetadataConfig.Value))!;
        var legacy = typeof(DisplayMetadataConfig).GetProperty(nameof(DisplayMetadataConfig.Legacy))!;
        var invalid = typeof(DisplayMetadataConfig).GetProperty(nameof(DisplayMetadataConfig.InvalidResource))!;
        Assert.Equal("Visible name", PropertyEditorHelper.GetDisplayName(null, value));
        Assert.Equal("Visible description", PropertyEditorHelper.GetDescription(null, value));
        Assert.Equal("Legacy name", PropertyEditorHelper.GetDisplayName(null, legacy));
        Assert.Equal("Legacy description", PropertyEditorHelper.GetDescription(null, legacy));
        Assert.Equal("InvalidResource", PropertyEditorHelper.GetDisplayName(null, invalid));
    }

    private sealed class TestConfig
    {
        [PropertyEditorType(typeof(ThrowingEditor), UpdateSourceTrigger = UpdateSourceTrigger.LostFocus)]
        public string Value { get; set; } = "Value";

        [ReadOnly(true)]
        public bool IsReadOnly { get; set; } = true;
    }

    public sealed class ThrowingEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
            => throw new InvalidOperationException("Expected test failure");
    }

    private interface ICustomValue { }

    private sealed class CustomValue : ICustomValue { }

    private enum TestEnum { Value }

    private sealed class MatcherEditor : IPropertyEditor
    {
        public MatcherEditor() { }

        public DockPanel GenProperties(PropertyInfo property, object obj) => new();
    }

    private sealed class ExactEditor : IPropertyEditor
    {
        public ExactEditor() { }

        public DockPanel GenProperties(PropertyInfo property, object obj) => new();
    }

    [Fact]
    public void PropertyBinding_UsesAttributeUpdateTriggerAndValidation()
    {
        PropertyInfo property = typeof(TestConfig).GetProperty(nameof(TestConfig.Value))!;

        Binding binding = PropertyEditorHelper.CreateTwoWayBinding(new TestConfig(), property);

        Assert.Equal(UpdateSourceTrigger.LostFocus, binding.UpdateSourceTrigger);
        Assert.True(binding.ValidatesOnExceptions);
        Assert.True(binding.ValidatesOnDataErrors);
        Assert.True(binding.NotifyOnValidationError);
    }

    [Fact]
    public void FailingAttributedEditor_FallsBackToStandardTypeEditor()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsurePropertyEditorResources();
            var config = new TestConfig();
            PropertyInfo property = typeof(TestConfig).GetProperty(nameof(TestConfig.Value))!;

            DockPanel panel = PropertyEditorHelper.GenProperties(property, config);

            Assert.Single(panel.Children.OfType<TextBox>());
        });
    }

    [Fact]
    public void ReadOnlyAttribute_DisablesGeneratedEditor()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsurePropertyEditorResources();
            var config = new TestConfig();
            PropertyInfo property = typeof(TestConfig).GetProperty(nameof(TestConfig.IsReadOnly))!;
            Assert.Equal(typeof(BoolPropertiesEditor), PropertyEditorHelper.GetEditorTypeForPropertyType(typeof(bool)));

            DockPanel panel = PropertyEditorHelper.GenProperties(property, config);

            Assert.False(panel.IsEnabled);
        });
    }

    [Fact]
    public void ExternalRegistration_ExactTypeWinsAndEditorIsReused()
    {
        PropertyEditorHelper.RegisterEditor<MatcherEditor>(type => typeof(ICustomValue).IsAssignableFrom(type));
        PropertyEditorHelper.RegisterEditor<ExactEditor>(typeof(CustomValue));

        Assert.Equal(typeof(ExactEditor), PropertyEditorHelper.GetEditorTypeForPropertyType(typeof(CustomValue)));
        Assert.Same(PropertyEditorHelper.GetOrCreateEditor<ExactEditor>(), PropertyEditorHelper.GetOrCreateEditor(typeof(ExactEditor)));
    }

    [Fact]
    public void BuiltInRegistrations_CoverTheFiniteStandardTypes()
    {
        (Type PropertyType, Type EditorType)[] registrations =
        [
            (typeof(string), typeof(TextboxPropertiesEditor)),
            (typeof(bool?), typeof(BoolPropertiesEditor)),
            (typeof(TestEnum), typeof(EnumPropertiesEditor)),
            (typeof(DateTime), typeof(TemporalPropertiesEditor)),
            (typeof(List<int>), typeof(CollectionJsonEditor)),
            (typeof(Dictionary<string, int>), typeof(DictionaryJsonEditor)),
            (typeof(Point), typeof(PointPropertiesEditor)),
            (typeof(Brush), typeof(BrushesPropertiesEditor)),
            (typeof(ICommand), typeof(CommandPropertiesEditor)),
            (typeof(Level), typeof(LevelPropertiesEditor)),
            (typeof(FontFamily), typeof(FontFamilyPropertiesEditor)),
            (typeof(FontWeight), typeof(FontWeightPropertiesEditor))
        ];

        foreach ((Type propertyType, Type editorType) in registrations)
            Assert.Equal(editorType, PropertyEditorHelper.GetEditorTypeForPropertyType(propertyType));
    }

    [Fact]
    public void PropertyEditorWindow_PublicApiKeepsCompatibilityAliases()
    {
        Type windowType = typeof(PropertyEditorWindow);

        Assert.NotNull(windowType.GetConstructor([typeof(object)]));
        Assert.NotNull(windowType.GetConstructor([typeof(object), typeof(PropertyEditorEditMode)]));

        ConstructorInfo legacyConstructor = windowType.GetConstructor([typeof(object), typeof(bool)])!;
        Assert.NotNull(legacyConstructor);
        Assert.NotNull(legacyConstructor.GetCustomAttribute<ObsoleteAttribute>());
        Assert.Equal(EditorBrowsableState.Never, legacyConstructor.GetCustomAttribute<EditorBrowsableAttribute>()?.State);

        Assert.NotNull(windowType.GetEvent(nameof(PropertyEditorWindow.Submitted)));
        EventInfo legacyEvent = windowType.GetEvent("Submited")!;
        Assert.NotNull(legacyEvent);
        Assert.NotNull(legacyEvent.GetCustomAttribute<ObsoleteAttribute>());
        Assert.Equal(EditorBrowsableState.Never, legacyEvent.GetCustomAttribute<EditorBrowsableAttribute>()?.State);
    }

    [Theory]
    [InlineData(PropertyEditorEditMode.Immediate)]
    [InlineData(PropertyEditorEditMode.Transactional)]
    public void MtfConditionalParametersAndCategoriesSurviveSearchAndSorting(PropertyEditorEditMode mode)
    {
        WpfTestHost.Invoke(() =>
        {
            EnsurePropertyEditorResources();
            StripeMtfParameters source = new() { Pattern = StripeMtfPattern.Horizontal, RectWidth = 73, TailRatio = .2 };
            PropertyEditorWindow window = new(source, mode);
            try
            {
                var editable = Assert.IsType<StripeMtfParameters>(window.EditConfig);
                var panel = Assert.IsType<StackPanel>(window.FindName("PropertyPanel"));
                var search = Assert.IsType<TextBox>(window.FindName("SearchBox"));
                Border Group(string name) => panel.Children.OfType<Border>().Single(b => Equals(b.Tag, name));
                IEnumerable<DockPanel> Rows() => panel.Children.OfType<Border>().SelectMany(b => ((StackPanel)b.Child).Children.OfType<DockPanel>());
                DockPanel Row(string name) => Rows().Single(p => p.Tag is PropertyInfo property && property.Name == name);
                void AssertFourPartVisibility(Visibility expected)
                {
                    Border group = Group("四部定位");
                    Assert.Equal(expected, group.Visibility);
                    Assert.Equal(expected == Visibility.Visible, window.TreeNodes.Single(n => n.Header == "四部定位").IsVisible);
                    foreach (DockPanel row in ((StackPanel)group.Child).Children.OfType<DockPanel>()) Assert.Equal(expected, row.Visibility);
                }

                Assert.False(editable.ShowAdvanced);
                foreach (StripeMtfPattern pattern in Enum.GetValues<StripeMtfPattern>())
                {
                    editable.Pattern = pattern;
                    Assert.Equal(new[] { nameof(StripeMtfParameters.Pattern), nameof(StripeMtfParameters.ShowAdvanced) },
                        Rows().Where(r => r.Visibility == Visibility.Visible).Select(r => ((PropertyInfo)r.Tag).Name));
                    AssertFourPartVisibility(Visibility.Collapsed);
                }
                editable.Pattern = StripeMtfPattern.Horizontal;
                editable.ShowAdvanced = true;
                AssertFourPartVisibility(Visibility.Collapsed);
                editable.Pattern = StripeMtfPattern.FourPart;
                AssertFourPartVisibility(Visibility.Visible);
                Assert.Equal(73, editable.RectWidth);
                editable.Pattern = StripeMtfPattern.Vertical;
                AssertFourPartVisibility(Visibility.Collapsed);
                search.Text = "四部定位";
                AssertFourPartVisibility(Visibility.Collapsed);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("SearchEmptyState")).Visibility);
                editable.Pattern = StripeMtfPattern.FourPart;
                AssertFourPartVisibility(Visibility.Visible);
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("SearchEmptyState")).Visibility);
                search.Text = "不存在的参数";
                editable.Pattern = StripeMtfPattern.Horizontal;
                editable.Pattern = StripeMtfPattern.FourPart;
                AssertFourPartVisibility(Visibility.Collapsed);
                search.Clear();
                AssertFourPartVisibility(Visibility.Visible);

                editable.Method = StripeMtfMethod.TrimmedExtrema;
                Assert.Equal(Visibility.Collapsed, Row(nameof(StripeMtfParameters.TailRatio)).Visibility);
                search.Text = "两端取样比例";
                Assert.Equal(Visibility.Collapsed, Row(nameof(StripeMtfParameters.TailRatio)).Visibility);
                editable.Method = StripeMtfMethod.TailMean;
                Assert.Equal(Visibility.Visible, Row(nameof(StripeMtfParameters.TailRatio)).Visibility);
                Assert.Equal(.2, editable.TailRatio);
                editable.ShowAdvanced = false;
                Assert.Equal(Visibility.Collapsed, Row(nameof(StripeMtfParameters.TailRatio)).Visibility);
                AssertFourPartVisibility(Visibility.Collapsed);
                editable.ShowAdvanced = true;
                Assert.Equal(Visibility.Visible, Row(nameof(StripeMtfParameters.TailRatio)).Visibility);
                Assert.Equal(.2, editable.TailRatio);
                Assert.Equal(73, editable.RectWidth);
                search.Clear();
                editable.Pattern = StripeMtfPattern.Horizontal;
                ((ComboBox)window.FindName("SortComboBox")).SelectedIndex = 1;
                AssertFourPartVisibility(Visibility.Collapsed);
                editable.Pattern = StripeMtfPattern.FourPart;
                AssertFourPartVisibility(Visibility.Visible);
                if (mode == PropertyEditorEditMode.Transactional) Assert.Equal(StripeMtfPattern.Horizontal, source.Pattern);
            }
            finally { window.Close(); }
        });
    }

    private sealed class NestedMtfConfig
    {
        public string Caption { get; set; } = "Fixture";
        public StripeMtfParameters Settings { get; set; } = new() { Pattern = StripeMtfPattern.Vertical, ShowAdvanced = true };
    }

    [Fact]
    public void NestedConditionalParametersKeepTheirOwnSourceAndFilterTheirNavigation()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsurePropertyEditorResources();
            NestedMtfConfig config = new();
            PropertyEditorWindow window = new(config);
            try
            {
                var panel = Assert.IsType<StackPanel>(window.FindName("PropertyPanel"));
                var search = Assert.IsType<TextBox>(window.FindName("SearchBox"));
                static IEnumerable<FrameworkElement> Descendants(FrameworkElement element)
                {
                    yield return element;
                    IEnumerable<FrameworkElement> children = element is Border border && border.Child is FrameworkElement content ? [content]
                        : element is Panel container ? container.Children.OfType<FrameworkElement>() : [];
                    foreach (var child in children) foreach (var descendant in Descendants(child)) yield return descendant;
                }
                Border locator = Descendants(panel).OfType<Border>().Single(b => Equals(b.Tag, "四部定位"));
                search.Text = "Settings";
                Assert.Equal(Visibility.Collapsed, locator.Visibility);
                search.Text = "四部定位";
                Assert.False(Assert.Single(window.TreeNodes).IsVisible);
                config.Settings.Pattern = StripeMtfPattern.FourPart;
                Assert.Equal(Visibility.Visible, locator.Visibility);
                Assert.True(Assert.Single(window.TreeNodes).IsVisible);
                Assert.True(Assert.Single(window.TreeNodes[0].Children).IsVisible);
                search.Clear();
                config.Settings.Pattern = StripeMtfPattern.Horizontal;
                Assert.Equal(Visibility.Collapsed, locator.Visibility);
            }
            finally { window.Close(); }
        });
    }

    private static void EnsurePropertyEditorResources()
    {
        Application application = Application.Current!;
        application.Resources["GlobalTextBrush"] = Brushes.Black;
        application.Resources["GlobalBorderBrush"] = Brushes.Transparent;
        application.Resources["BorderBrush"] = Brushes.Gray;
        application.Resources["ButtonCommand"] = new Style(typeof(Button));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ButtonDefault"] = new Style(typeof(Button));
        application.Resources["ButtonPrimary"] = new Style(typeof(Button));
        application.Resources["TextBoxBaseStyle"] = new Style(typeof(TextBox));
        application.Resources["TreeViewItemBaseStyle"] = new Style(typeof(TreeViewItem));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
