using ColorVision.Engine.Templates.Browser;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowTemplateBrowserOrderTests
{
    [Fact]
    public async Task BrowserOrdersAreIsolatedBySourceAndDoNotCreateTemplateRecords()
    {
        string path = Path.Combine(Path.GetTempPath(), "ColorVision-flow-order-tests", Guid.NewGuid().ToString("N"), "browser.db");
        var store = new TemplateBrowserOrderStore(path);
        Assert.Empty(await store.LoadAsync("local"));
        string first = TemplateBrowserOrderStore.MySqlScope("db", 3306, "one");
        string second = TemplateBrowserOrderStore.MySqlScope("db", 3306, "two");
        Assert.NotEqual(first, second);
        Assert.Equal(first, TemplateBrowserOrderStore.MySqlScope("DB", 3306, "one"));
        await store.SaveAsync(first, ["id:10", "id:5"]);
        await store.SaveAsync(second, ["id:5", "id:10"]);
        Assert.Equal(new[] { "id:10", "id:5" }, await new TemplateBrowserOrderStore(path).LoadAsync(first));
        Assert.Equal(new[] { "id:5", "id:10" }, await store.LoadAsync(second));
        Assert.Empty(await store.LoadAsync("local"));
        using var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False");
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
        Assert.Equal("flow_template_browser_order", command.ExecuteScalar());
    }

    [Fact]
    public async Task UnavailableStoreReportsFailureInsteadOfPretendingOrderWasSaved()
    {
        string path = Path.GetTempFileName();
        await Assert.ThrowsAnyAsync<IOException>(() => new TemplateBrowserOrderStore(Path.Combine(path, "browser.db")).SaveAsync("local", ["id:1"]));
    }
}
