#pragma warning disable CA1707
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow.NodeConfigurator;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class NodeConfiguratorBindingTests
{
    private sealed class AdvancedFilterConfig
    {
        public string StandardValue { get; set; } = "Standard";
        public string AdvancedValue { get; set; } = "Advanced";
    }

    [Fact]
    public void PropertyEditors_BindAndFilterAdvancedProperties()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application application = Application.Current ?? new Application();
                application.Resources["GlobalTextBrush"] = Brushes.Black;
                application.Resources["GlobalBorderBrush"] = Brushes.Transparent;
                application.Resources["BorderBrush"] = Brushes.Gray;
                application.Resources["PrimaryBrush"] = Brushes.DodgerBlue;
                application.Resources["GlobalBackground"] = Brushes.White;
                application.Resources["PrimaryTextBrush"] = Brushes.Black;
                application.Resources["SecondaryTextBrush"] = Brushes.Gray;
                application.Resources["ButtonCommand"] = new Style(typeof(Button));
                application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
                application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
                application.Resources["ComboBoxPlus.Small"] = new Style(typeof(HandyControl.Controls.ComboBox));
                application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();

                var node = new STNodeHub { Title = "First" };
                var panel = new StackPanel();
                var context = new NodeConfiguratorContext
                {
                    Node = node,
                    SignStackPanel = panel
                };
                var templates = new ObservableCollection<TemplateModel<ParamModBase>>
                {
                    new("First", new ParamModBase { Name = "First" }),
                    new("Second", new ParamModBase { Name = "Second" })
                };

                context.AddTemplateCollectionPanel(nameof(node.Title), "Template", templates);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

                var selectorPanel = Assert.IsType<DockPanel>(Assert.Single(panel.Children));
                var comboBox = Assert.Single(selectorPanel.Children.OfType<HandyControl.Controls.ComboBox>());
                Assert.Equal("First", comboBox.SelectedValue);

                comboBox.SelectedValue = "Second";
                comboBox.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateSource();
                Assert.Equal("Second", node.Title);

                node.Title = "First";
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Equal("First", comboBox.SelectedValue);

                comboBox.SelectedIndex = -1;
                comboBox.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateSource();
                Assert.Equal(string.Empty, node.Title);

                var filterConfig = new AdvancedFilterConfig();
                var advancedOptions = new PropertyEditorAdvancedOptions(property => property.Name == nameof(AdvancedFilterConfig.AdvancedValue));
                StackPanel propertyEditor = PropertyEditorHelper.GenPropertyEditorControl(filterConfig, advancedOptions: advancedOptions);

                Assert.Equal(new[] { nameof(AdvancedFilterConfig.StandardValue) }, GetDisplayedPropertyNames(propertyEditor));
                ToggleButton advancedToggle = Assert.Single(FindVisualChildren<ToggleButton>(propertyEditor));
                Assert.Equal(Dock.Right, DockPanel.GetDock(advancedToggle));

                advancedToggle.IsChecked = true;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.True(advancedOptions.ShowAdvancedProperties);
                Assert.Equal(
                    new[] { nameof(AdvancedFilterConfig.AdvancedValue), nameof(AdvancedFilterConfig.StandardValue) }.Order(),
                    GetDisplayedPropertyNames(propertyEditor).Order());

                advancedToggle = Assert.Single(FindVisualChildren<ToggleButton>(propertyEditor));
                advancedToggle.IsChecked = false;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.False(advancedOptions.ShowAdvancedProperties);
                Assert.Equal(new[] { nameof(AdvancedFilterConfig.StandardValue) }, GetDisplayedPropertyNames(propertyEditor));

                StackPanel defaultEditor = PropertyEditorHelper.GenPropertyEditorControl(filterConfig);
                Assert.Empty(FindVisualChildren<ToggleButton>(defaultEditor));
                Assert.Equal(2, GetDisplayedPropertyNames(defaultEditor).Count);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static List<string> GetDisplayedPropertyNames(DependencyObject root)
    {
        return FindVisualChildren<DockPanel>(root)
            .Select(panel => panel.Tag)
            .OfType<PropertyInfo>()
            .Select(property => property.Name)
            .ToList();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;

            foreach (T descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
