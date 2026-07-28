using ColorVision.Engine.FlowProcessing.PostProcess;

namespace ColorVision.UI.Tests;

public sealed class PostProcessTypeCatalogTests
{
    [Fact]
    public void CreateOptionsUsesDeclaredCategoriesAndRemovesDuplicateTypes()
    {
        IReadOnlyList<PostProcessTypeOption> options = PostProcessTypeCatalog.CreateOptions(
        [
            new LaterPostProcessor(),
            new CategorizedPostProcessor(),
            new CategorizedPostProcessor()
        ]);

        Assert.Equal(2, options.Count);
        PostProcessTypeOption categorized = Assert.Single(options, option => option.Process is CategorizedPostProcessor);
        Assert.Equal("测试分类", categorized.Category);
        Assert.Equal("分类处理", categorized.DisplayName);
        Assert.Equal("支持处理配置", categorized.ConfigurationSummary);
    }

    [Fact]
    public void CreateOptionsSortsTypesByMetadataOrderWithinCategory()
    {
        IReadOnlyList<PostProcessTypeOption> options = PostProcessTypeCatalog.CreateOptions(
        [
            new LaterPostProcessor(),
            new CategorizedPostProcessor()
        ]);

        Assert.Collection(
            options,
            option => Assert.IsType<CategorizedPostProcessor>(option.Process),
            option => Assert.IsType<LaterPostProcessor>(option.Process));
    }

    [PostProcess("分类处理", "用于验证分类卡片", Category = "测试分类", Order = 10)]
    private sealed class CategorizedPostProcessor : IPostProcessor
    {
        public bool Process(PostProcessContext ctx) => true;
        public object GetConfig() => new object();
    }

    [PostProcess("后续处理", Category = "测试分类", Order = 20)]
    private sealed class LaterPostProcessor : IPostProcessor
    {
        public bool Process(PostProcessContext ctx) => true;
    }
}
