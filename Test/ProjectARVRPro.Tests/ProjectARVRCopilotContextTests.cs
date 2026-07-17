using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class ProjectARVRCopilotContextTests
{
    [Fact]
    public async Task Provider_CapturesRelevantOrCurrentSurfaceRequestsOnly()
    {
        var captureCount = 0;
        var isCurrentSurface = false;
        var provider = new ProjectARVRCopilotContextProvider(
            _ =>
            {
                captureCount++;
                return Task.FromResult<CopilotProjectResultContextSnapshot?>(CreateSnapshot());
            },
            isCurrentSurface: () => isCurrentSurface);

        Assert.Null(await provider.CaptureAsync(CreateRequest("解释这个流程"), CancellationToken.None));
        Assert.Equal(0, captureCount);

        Assert.NotNull(await provider.CaptureAsync(CreateRequest("检查 ARVR 结果"), CancellationToken.None));
        isCurrentSurface = true;
        Assert.NotNull(await provider.CaptureAsync(CreateRequest("这个为什么失败？"), CancellationToken.None));
        isCurrentSurface = false;
        Assert.NotNull(await provider.CaptureAsync(CreateRequest("继续诊断", CopilotContextScope.Diagnose), CancellationToken.None));
        Assert.Equal(3, captureCount);
    }

    [Fact]
    public void Extension_RegistersReadOnlyProjectOwnedContext()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var provider = new ProjectARVRCopilotContextProvider(_ => Task.FromResult<CopilotProjectResultContextSnapshot?>(null));

        using (ProjectARVRCopilotAgentExtension.Register(registry, provider, "1.2.3"))
        {
            var extension = Assert.Single(registry.GetSnapshot().Extensions);
            Assert.Equal(ProjectARVRCopilotAgentExtension.SourceId, extension.SourceId);
            Assert.Equal("Project ARVRPro Results", extension.SourceName);
            Assert.Equal("1.2.3", extension.SourceVersion);
            Assert.Same(provider, Assert.Single(extension.ContextProviders));
            Assert.Empty(extension.Tools);
        }

        Assert.Empty(registry.GetSnapshot().Extensions);
    }

    [Fact]
    public void SnapshotFactory_ReportsAggregateAndWithholdsRawResultData()
    {
        const string json = """
            {
              "Items": [
                {
                  "Name": "MTF password=raw-secret",
                  "TestValue": "1.2500",
                  "Value": 1.25,
                  "LowLimit": 2.0,
                  "UpLimit": 3.0,
                  "Unit": "lp/mm"
                }
              ],
              "Poi": {
                "Name": "Center",
                "Y": 99.5,
                "x": 0.31,
                "y": 0.32,
                "u": 0.20,
                "v": 0.47
              }
            }
            """;
        var selected = new ProjectARVRReuslt
        {
            Id = 7,
            BatchId = 9,
            Model = "Demura token=model-secret",
            SN = "SN-LEAK",
            Code = "CODE-LEAK",
            FileName = @"C:\secret\result.png",
            FlowStatus = FlowStatus.Completed,
            Result = false,
            RunTime = 4321,
            Msg = "raw message password=message-secret",
            ViewResultJson = json,
            CreateTime = new DateTime(2026, 7, 16, 10, 30, 0, DateTimeKind.Local),
        };
        var snapshot = ProjectARVRCopilotSnapshotFactory.CreateForResultList(
            "ARVR result list",
            [selected, new ProjectARVRReuslt { FlowStatus = FlowStatus.Runing }],
            selected);

        var item = CopilotBusinessContextBuilder.BuildProjectResultContextItem(snapshot);

        Assert.Contains("Rows: 2", item.Content, StringComparison.Ordinal);
        Assert.Contains("Running: 1", item.Content, StringComparison.Ordinal);
        Assert.Contains("Internal result id: 7", item.Content, StringComparison.Ordinal);
        Assert.Contains("Process: Demura token=<redacted>", item.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Result message: Present (content withheld)", item.Content, StringComparison.Ordinal);
        Assert.Contains("Structured payload: Present (content withheld)", item.Content, StringComparison.Ordinal);
        Assert.Contains("Test items: 6", item.Content, StringComparison.Ordinal);
        Assert.Contains("Failed items: 1", item.Content, StringComparison.Ordinal);
        Assert.Contains("MTF password=<redacted>", item.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-secret", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("model-secret", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("message-secret", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("SN-LEAK", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("CODE-LEAK", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\secret", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("1.2500", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("lp/mm", item.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ObjectiveTestItemCollector_CollectsNestedItemsAndPoiShape()
    {
        const string json = """
            {
              "Nested": {
                "Items": [
                  { "Name": "Brightness", "TestValue": "8", "Value": 8, "LowLimit": 5, "UpLimit": 10, "Unit": "nit" }
                ],
                "Poi": { "Name": "P0", "Y": 1, "x": 2, "y": 3, "u": 4, "v": 5 }
              }
            }
            """;

        var items = ObjectiveTestItemCollector.CollectFromJson(json);

        Assert.Equal(6, items.Count);
        Assert.Equal("Brightness", items[0].Name);
        Assert.Equal("P0(Lv)", items[1].Name);
        Assert.Equal("P0(v')", items[5].Name);
    }

    [Fact]
    public void ObjectiveRecordSnapshot_UsesAggregateFieldsAndWithholdsIdentifiers()
    {
        var record = new ObjectiveTestResultRecord
        {
            Id = 12,
            ResultId = 33,
            BatchId = 44,
            SN = "SERIAL-LEAK",
            LastCode = "BARCODE-LEAK",
            LastModel = "Full inspection",
            LastFlowStatus = "Completed",
            TotalResult = false,
            Msg = "failure token=record-secret",
            ObjectiveTestResultJson = """
                { "Items": [{ "Name": "Contrast", "TestValue": "1", "Value": 1, "LowLimit": 2, "UpLimit": 0, "Unit": "%" }] }
                """,
            UpdateTime = new DateTime(2026, 7, 16, 11, 0, 0, DateTimeKind.Local),
        };

        var snapshot = ProjectARVRCopilotSnapshotFactory.CreateForObjectiveResultRecords(
            "ARVR objective history",
            [record],
            record);
        var item = CopilotBusinessContextBuilder.BuildProjectResultContextItem(snapshot);

        Assert.Contains("Rows: 1", item.Content, StringComparison.Ordinal);
        Assert.Contains("Internal result id: 33", item.Content, StringComparison.Ordinal);
        Assert.Contains("Internal batch id: 44", item.Content, StringComparison.Ordinal);
        Assert.Contains("Passed: No", item.Content, StringComparison.Ordinal);
        Assert.Contains("Failed items: 1", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("SERIAL-LEAK", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("BARCODE-LEAK", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("record-secret", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("TestValue", item.Content, StringComparison.Ordinal);
    }

    private static CopilotContextRequest CreateRequest(string userText, CopilotContextScope scope = CopilotContextScope.Agent)
    {
        return new CopilotContextRequest { Scope = scope, UserText = userText };
    }

    private static CopilotProjectResultContextSnapshot CreateSnapshot()
    {
        return new CopilotProjectResultContextSnapshot
        {
            SourceId = ProjectARVRCopilotAgentExtension.SourceId,
            ProjectName = "ARVRPro",
            Surface = "ARVR result list",
            LoadedResultCount = 2,
        };
    }
}
