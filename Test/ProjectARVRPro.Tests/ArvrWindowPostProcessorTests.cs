using ColorVision.Engine.FlowProcessing.PostProcess;
using ProjectARVRPro.Process;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class ArvrWindowPostProcessorTests
{
    [Fact]
    public void AdapterPublishesArvrMetadataForTheMainPostProcessEditor()
    {
        var processor = new ArvrWindowPostProcessor();

        PostProcessMetadata metadata = PostProcessMetadata.FromProcess(processor);

        Assert.Equal("ARVR结果查看", metadata.DisplayName);
        Assert.Equal(PostProcessTypeCatalog.ArvrCategory, metadata.Category);
        Assert.False(processor.Process(null!));
    }
}
