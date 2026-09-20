using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.UI.Tests;

public class CameraTemplateSelectionTests
{
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
