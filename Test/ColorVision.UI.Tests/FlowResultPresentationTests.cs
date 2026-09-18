using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.UI;
using Newtonsoft.Json;
using SqlSugar;

namespace ColorVision.UI.Tests;

public sealed class FlowResultPresentationTests
{
    [Fact]
    public void LegacyQuerySettings_PreserveStoredValuesAndExposeOnlyEffectiveOptions()
    {
        var config = JsonConvert.DeserializeObject<MeasureBatchManagerPageConfig>(
            """{"Count":120,"OrderByType":0,"AutoRefreshView":false,"InsertAtBeginning":false}""")!;
        Assert.Equal(120, config.Count);
        Assert.Equal(OrderByType.Asc, config.OrderByType);
        Assert.False(config.AutoRefreshView);
        Assert.False(config.InsertAtBeginning);
        Assert.Equal(new[] { "Count", "OrderByType" }, PropertyEditorHelper.GetEditableProperties(config.GetType()).Select(property => property.Name));
        var roundTrip = JsonConvert.DeserializeObject<MeasureBatchManagerPageConfig>(JsonConvert.SerializeObject(config))!;
        Assert.Equal(config.Count, roundTrip.Count);
        Assert.False(roundTrip.AutoRefreshView);
    }

    [Fact]
    public void BatchDuration_ConvertsStoredMillisecondsWithoutRoundingTheSource()
    {
        var row = new ViewBatchResult { MeasureBatchModel = new MeasureBatchModel { TotalTime = 2364, FlowStatus = FlowStatus.Failed } };
        Assert.Equal(2.364, row.DurationSeconds);
        Assert.Equal(2364, row.MeasureBatchModel.TotalTime);
    }

    [Fact]
    public void FailedHistoryQueryMarksRetainedCopilotRowsAsStale()
    {
        var item = CopilotBusinessContextBuilder.BuildMeasurementResultContextItem(
            new CopilotMeasurementResultContextSnapshot
            {
                Surface = "Measurement result history",
                LoadedBatchCount = 4,
                IsFilterActive = true,
                LastQuerySucceeded = false,
                IsLoadedDataStale = true,
                RequestedFilterMatchesLoadedData = false,
                LoadedDataAsOf = "2026-09-18T01:02:03.0000000Z",
            });

        Assert.Contains("Last query: Failed", item.Content, StringComparison.Ordinal);
        Assert.Contains("Stale (last query failed; loaded rows may predate it)", item.Content, StringComparison.Ordinal);
        Assert.Contains("Requested filter matches loaded data: No", item.Content, StringComparison.Ordinal);
        Assert.Contains("stale retained data", item.Summary, StringComparison.Ordinal);
    }
}
