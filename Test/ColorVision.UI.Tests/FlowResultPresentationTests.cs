using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
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
}
