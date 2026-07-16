using ColorVision.Database;
using ColorVision.UI;

namespace ColorVision.UI.Tests;

public sealed class CopilotDatabaseContextProviderTests
{
    [Fact]
    public async Task DatabaseProvider_CapturesFreshSnapshotForRelevantRequests()
    {
        var tableName = "inspection_result_a";
        var captureCount = 0;
        var provider = new CopilotDatabaseContextProvider(_ =>
        {
            captureCount++;
            return Task.FromResult<CopilotDatabaseContextSnapshot?>(CreateSnapshot(tableName));
        });

        var first = await provider.CaptureAsync(CreateRequest("检查当前数据库表"), CancellationToken.None);
        tableName = "inspection_result_b";
        var second = await provider.CaptureAsync(CreateRequest("describe the query result schema"), CancellationToken.None);

        Assert.Equal(2, captureCount);
        Assert.Contains("inspection_result_a", Assert.IsType<CopilotContextItem>(first).Content, StringComparison.Ordinal);
        Assert.Contains("inspection_result_b", Assert.IsType<CopilotContextItem>(second).Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseProvider_SkipsUnrelatedTurnsButSupportsCurrentSurfaceAndDiagnose()
    {
        var captureCount = 0;
        var isCurrentSurface = false;
        var provider = new CopilotDatabaseContextProvider(
            _ =>
            {
                captureCount++;
                return Task.FromResult<CopilotDatabaseContextSnapshot?>(CreateSnapshot("results"));
            },
            isCurrentSurface: () => isCurrentSurface);

        Assert.Null(await provider.CaptureAsync(CreateRequest("解释这段代码"), CancellationToken.None));
        Assert.Equal(0, captureCount);

        isCurrentSurface = true;
        Assert.NotNull(await provider.CaptureAsync(CreateRequest("它现在怎么样？"), CancellationToken.None));
        isCurrentSurface = false;
        Assert.NotNull(await provider.CaptureAsync(CreateRequest("继续排查", CopilotContextScope.Diagnose), CancellationToken.None));
        Assert.Equal(2, captureCount);
    }

    [Fact]
    public async Task DatabaseProvider_DropsSnapshotWhenSourceBecomesInactiveDuringCapture()
    {
        var active = true;
        var provider = new CopilotDatabaseContextProvider(
            _ =>
            {
                active = false;
                return Task.FromResult<CopilotDatabaseContextSnapshot?>(CreateSnapshot("removed_table"));
            },
            () => active);

        var result = await provider.CaptureAsync(CreateRequest("检查数据库"), CancellationToken.None);

        Assert.Null(result);
        Assert.False(provider.CanProvide(CopilotContextScope.Agent));
    }

    [Fact]
    public void DatabaseExtension_UsesStableSourceMetadataAndRegistrationLifetime()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var provider = new CopilotDatabaseContextProvider(_ => Task.FromResult<CopilotDatabaseContextSnapshot?>(null));

        using (CopilotDatabaseBrowserAgentExtension.Register(registry, provider, "1.2.3"))
        {
            var extension = Assert.Single(registry.GetSnapshot().Extensions);
            Assert.Equal(CopilotDatabaseBrowserAgentExtension.SourceId, extension.SourceId);
            Assert.Equal("Database Browser", extension.SourceName);
            Assert.Equal("1.2.3", extension.SourceVersion);
            Assert.Same(provider, Assert.Single(extension.ContextProviders));
            Assert.Empty(extension.Tools);
        }

        Assert.Empty(registry.GetSnapshot().Extensions);
    }

    [Fact]
    public async Task DatabaseCoordinator_KeepsOneExtensionAndFollowsActiveSession()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var coordinator = new CopilotDatabaseContextCoordinator(registry);
        using var firstSession = coordinator.Register(_ => Task.FromResult<CopilotDatabaseContextSnapshot?>(CreateSnapshot("first")), "4.0");
        using var secondSession = coordinator.Register(_ => Task.FromResult<CopilotDatabaseContextSnapshot?>(CreateSnapshot("second")), "4.0");
        var extension = Assert.Single(registry.GetSnapshot().Extensions);
        var provider = Assert.Single(extension.ContextProviders);

        var second = await provider.CaptureAsync(CreateRequest("检查数据库"), CancellationToken.None);
        firstSession.Activate();
        var first = await provider.CaptureAsync(CreateRequest("检查数据库"), CancellationToken.None);

        Assert.Contains("second", Assert.IsType<CopilotContextItem>(second).Content, StringComparison.Ordinal);
        Assert.Contains("first", Assert.IsType<CopilotContextItem>(first).Content, StringComparison.Ordinal);
        firstSession.Dispose();
        Assert.Single(registry.GetSnapshot().Extensions);
        secondSession.Dispose();
        Assert.Empty(registry.GetSnapshot().Extensions);
    }

    [Fact]
    public async Task DatabaseCoordinator_DropsCaptureFromSessionClosedWhileAwaiting()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var coordinator = new CopilotDatabaseContextCoordinator(registry);
        var completion = new TaskCompletionSource<CopilotDatabaseContextSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = coordinator.Register(_ => completion.Task);
        var provider = Assert.Single(Assert.Single(registry.GetSnapshot().Extensions).ContextProviders);

        var capture = provider.CaptureAsync(CreateRequest("检查数据库"), CancellationToken.None);
        session.Dispose();
        completion.SetResult(CreateSnapshot("closed"));

        Assert.Null(await capture);
        Assert.Empty(registry.GetSnapshot().Extensions);
    }

    [Fact]
    public void DatabaseContextBuilder_ExposesOnlyBoundedShapeAndMasksInlineSecrets()
    {
        var snapshot = new CopilotDatabaseContextSnapshot
        {
            ConnectionState = "Current page query succeeded",
            ProviderName = "MySQL",
            DatabaseType = "MySql",
            DatabaseName = "production pwd=db-secret",
            TableName = "orders password=table-secret",
            HasLoadedPage = true,
            QueryTotalCount = 120,
            LoadedRowCount = 50,
            PageIndex = 1,
            PageSize = 50,
            TotalPages = 3,
            SortColumn = "id",
            SortDirection = "Descending",
            HasSearchFilter = true,
            Columns = Enumerable.Range(0, 65).Select(index => new CopilotDatabaseColumnContextSnapshot
            {
                ColumnName = $"column-{index:D3}",
                StoreType = "varchar(64)",
                Ordinal = index,
                IsNullable = index % 2 == 0,
                Comment = index == 0 ? "api_key:column-secret" : string.Empty,
            }).ToArray(),
        };

        var item = CopilotBusinessContextBuilder.BuildDatabaseContextItem(snapshot);

        Assert.Contains("password=<redacted>", item.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pwd=<redacted>", item.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("api_key=<redacted>", item.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("table-secret", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("db-secret", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("column-secret", item.Content, StringComparison.Ordinal);
        Assert.Contains("Search filter active: Yes (term withheld)", item.Content, StringComparison.Ordinal);
        Assert.Contains("- column-059", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("- column-060 /", item.Content, StringComparison.Ordinal);
        Assert.Contains("5 more columns omitted", item.Content, StringComparison.Ordinal);
        Assert.True(item.Content.Length <= 16000);
    }

    private static CopilotContextRequest CreateRequest(string userText, CopilotContextScope scope = CopilotContextScope.Agent)
    {
        return new CopilotContextRequest { Scope = scope, UserText = userText };
    }

    private static CopilotDatabaseContextSnapshot CreateSnapshot(string tableName)
    {
        return new CopilotDatabaseContextSnapshot
        {
            ConnectionState = "Current page query succeeded",
            ProviderName = "MySQL",
            DatabaseType = "MySql",
            DatabaseName = "colorvision",
            TableName = tableName,
            HasLoadedPage = true,
            QueryTotalCount = 120,
            LoadedRowCount = 50,
            PageIndex = 1,
            PageSize = 50,
            TotalPages = 3,
            SortColumn = "id",
            SortDirection = "Descending",
            HasPrimaryKey = true,
            CanWrite = true,
            Columns =
            [
                new CopilotDatabaseColumnContextSnapshot
                {
                    ColumnName = "id",
                    StoreType = "bigint",
                    Ordinal = 0,
                    IsPrimaryKey = true,
                    IsIdentity = true,
                },
            ],
        };
    }
}
