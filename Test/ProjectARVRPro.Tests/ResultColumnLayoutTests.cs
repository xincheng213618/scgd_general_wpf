using ColorVision.Testing;
using Newtonsoft.Json;
using System.Windows.Controls;

namespace ProjectARVRPro.Tests;

public sealed class ResultColumnLayoutTests
{
    [Fact]
    public void DraggedOrderSurvivesConfigurationRoundTrip()
    {
        StaTest.Run(() =>
        {
            var columns = CreateColumns();
            columns.Move(3, 0);
            var config = new ARVRWindowConfig { ResultColumnOrder = ResultColumnLayout.Capture(columns) };
            string json = JsonConvert.SerializeObject(config);
            var saved = JsonConvert.DeserializeObject<ARVRWindowConfig>(json)!;

            var reopenedColumns = CreateColumns();
            ResultColumnLayout.Restore(reopenedColumns, saved.ResultColumnOrder);

            Assert.Equal(["SN", "Id", "FlowStatus", "Result"], ResultColumnLayout.Capture(reopenedColumns));
        });
    }

    [Fact]
    public void RestoreKeepsDefaultOrderForOldSettingsAndAppendsUnlistedColumns()
    {
        StaTest.Run(() =>
        {
            var columns = CreateColumns();
            var oldConfig = JsonConvert.DeserializeObject<ARVRWindowConfig>("{}")!;
            ResultColumnLayout.Restore(columns, oldConfig.ResultColumnOrder);
            Assert.Equal(["Id", "FlowStatus", "Result", "SN"], ResultColumnLayout.Capture(columns));

            ResultColumnLayout.Restore(columns, ["obsolete", "SN", "Result", "SN"]);

            Assert.Equal(["SN", "Result", "Id", "FlowStatus"], ResultColumnLayout.Capture(columns));
        });
    }

    private static GridViewColumnCollection CreateColumns()
    {
        var grid = new GridView();
        foreach (string id in new[] { "Id", "FlowStatus", "Result", "SN" })
        {
            var column = new GridViewColumn { Header = $"Label for {id}" };
            ResultColumnLayout.SetColumnId(column, id);
            grid.Columns.Add(column);
        }
        return grid.Columns;
    }
}
