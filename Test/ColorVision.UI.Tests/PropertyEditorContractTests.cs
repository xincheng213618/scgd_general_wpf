using ColorVision.UI.LogImp;
using log4net.Core;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public class PropertyEditorContractTests
{
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

    private static void EnsurePropertyEditorResources()
    {
        Application application = Application.Current!;
        application.Resources["GlobalTextBrush"] = Brushes.Black;
        application.Resources["GlobalBorderBrush"] = Brushes.Transparent;
        application.Resources["BorderBrush"] = Brushes.Gray;
        application.Resources["ButtonCommand"] = new Style(typeof(Button));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
