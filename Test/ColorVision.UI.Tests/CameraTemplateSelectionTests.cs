using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class CameraTemplateSelectionTests
{
    [Fact]
    public void AutoFocusSelectionUsesReloadedTemplateWithoutRestartingThePanel()
    {
        StaTest.Run(() =>
        {
            var item = new TemplateModel<AutoFocusParam>("Focus", new AutoFocusParam { Id = 1, Name = "Focus", CurStep = 10000 });
            var selection = new ComboBox { ItemsSource = new ObservableCollection<TemplateModel<AutoFocusParam>> { item }, SelectedValuePath = "Value", SelectedIndex = 0 };
            Assert.Same(item.Value, selection.SelectedValue);
            var reloaded = new AutoFocusParam { Id = 1, Name = "Focus", CurStep = 5000 };
            item.Value = reloaded;
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            // Match the button path and resolve the current model after the editor reloads templates.
            Assert.True(CameraTemplateSelection.TryResolveRequired(selection.SelectedItem, out AutoFocusParam selected));
            Assert.Same(reloaded, selected);
        });
    }

    [Fact]
    public void OptionalTemplateFallsBackToExplicitEmpty()
    {
        ParamBase missing = CameraTemplateSelection.ResolveOptional<ParamBase>(null);
        ParamBase uninitialized = CameraTemplateSelection.ResolveOptional<ParamBase>(new ParamBase());

        Assert.Equal(-1, missing.Id);
        Assert.Equal("Empty", missing.Name);
        Assert.Equal(-1, uninitialized.Id);
        Assert.Equal("Empty", uninitialized.Name);
        Assert.False(CameraTemplateSelection.TryResolveRequired<ParamBase>(null, out _));
        Assert.False(CameraTemplateSelection.TryResolveRequired<ParamBase>(new ParamBase { Id = -1, Name = "Empty" }, out _));
        Assert.False(CameraTemplateSelection.TryResolveRequired<ParamBase>(new ParamBase(), out _));
    }

    [Fact]
    public void OptionalTemplatePreservesConfiguredTemplate()
    {
        ParamBase configured = new() { Id = 17, Name = "Capture" };

        Assert.Same(configured, CameraTemplateSelection.ResolveOptional<ParamBase>(configured));
        Assert.True(CameraTemplateSelection.TryResolveRequired(configured, out ParamBase selected));
        Assert.Same(configured, selected);
    }

    [Fact]
    public void EmptyCollectionAlwaysContainsExplicitEmptyTemplate()
    {
        StaTest.Run(() =>
        {
            ObservableCollection<TemplateModel<ParamBase>> source =
            [
                new TemplateModel<ParamBase>("Configured", new ParamBase { Id = 7 })
            ];
            ObservableCollection<TemplateModel<ParamBase>> options = source.CreateEmpty();

            source.Clear();

            TemplateModel<ParamBase> empty = Assert.Single(options);
            Assert.Equal("Empty", empty.Key);
            Assert.Equal(-1, empty.Value.Id);
            Assert.Equal("Empty", empty.Value.Name);
        });
    }
}
