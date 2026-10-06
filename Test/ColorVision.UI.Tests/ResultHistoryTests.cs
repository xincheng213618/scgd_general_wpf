using ColorVision.Core;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.Results;
using ColorVision.Database;
using Newtonsoft.Json;
using SqlSugar;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class ResultHistoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StreamingHistoryStaysBoundedAndSortedDisplayDoesNotChangeRetention(bool prepend)
    {
        var history = new ResultHistoryCollection<Row>(row => row.Id, 3);
        history.CollectionChanged += (_, _) => Assert.InRange(history.Count, 0, 3);
        for (int id = 1; id <= 20; id++)
        {
            if (prepend) history.Insert(0, new Row { Id = id });
            else history.Add(new Row { Id = id });
            if (history.Count > 1) history.Move(0, history.Count - 1);
        }
        Assert.Equal(new[] { 18, 19, 20 }, history.Select(row => row.Id).Order());
        history.Add(new Row { Id = 20 });
        history.Add(new Row { Id = 2 });
        Assert.Equal(new[] { 18, 19, 20 }, history.Select(row => row.Id).Order());
        history.MaxCount = 1;
        Assert.Equal(20, Assert.Single(history).Id);
        history.Clear();
        Assert.False(history.ContainsId(20));
        history.Add(new Row { Id = 2 });
        Assert.Equal(2, Assert.Single(history).Id);
    }

    [Fact]
    public void DeletingAndReplacingRowsKeepTheDeduplicationIndexConsistent()
    {
        var history = new ResultHistoryCollection<Row>(row => row.Id, 3) { new() { Id = 1 }, new() { Id = 2 } };
        history.RemoveAt(0);
        history.Add(new Row { Id = 1 });
        history[0] = new Row { Id = 3 };
        Assert.False(history.ContainsId(2));
        Assert.True(history.ContainsId(3));
        Assert.Throws<InvalidOperationException>(() => history[0] = new Row { Id = 1 });
        Assert.Equal(new[] { 3, 1 }, history.Select(row => row.Id));
    }

    [Fact]
    public void EvictingASelectedResultClearsTheListSelection()
    {
        StaTest.Run(() =>
        {
            var history = new ResultHistoryCollection<Row>(row => row.Id, 1) { new() { Id = 1 } };
            ListView list = new() { ItemsSource = history, SelectedItem = history[0] };
            history.Add(new Row { Id = 2 });
            Assert.Null(list.SelectedItem);
            Assert.Equal(-1, list.SelectedIndex);
            Assert.Equal(2, Assert.IsType<Row>(Assert.Single(list.Items.Cast<object>())).Id);
            list.ItemsSource = null;
        });
    }

    [Fact]
    public void EvictionAndClearReleaseResultsFromTheIndex()
    {
        var history = new ResultHistoryCollection<Row>(row => row.Id, 1);
        WeakReference evicted = AddTrackedRow(history, 1);
        WeakReference cleared = AddTrackedRow(history, 2);
        WeakReference queried = AddTrackedRow(history, 3, queried: true);
        history.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(evicted.IsAlive);
        Assert.False(cleared.IsAlive);
        Assert.False(queried.IsAlive);
        GC.KeepAlive(history);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddTrackedRow(ResultHistoryCollection<Row> history, int id, bool queried = false)
    {
        Row row = new() { Id = id };
        if (queried) history.QueryResults.Add(row);
        else history.Add(row);
        return new WeakReference(row);
    }

    [Fact]
    public void LegacyConfigurationGetsAFiniteLimitAndNonpositiveValuesCannotDisableIt()
    {
        ViewCameraConfig camera = JsonConvert.DeserializeObject<ViewCameraConfig>("{\"Count\":0}")!;
        ViewAlgorithmConfig algorithm = JsonConvert.DeserializeObject<ViewAlgorithmConfig>("{}")!;
        Assert.Equal(500, camera.MaxHistoryCount);
        Assert.Equal(500, algorithm.MaxHistoryCount);
        Assert.Equal(0, camera.Count);
        camera.MaxHistoryCount = 0;
        Assert.Equal(1, camera.MaxHistoryCount);
        algorithm.Count = 10000;
        Assert.Equal(10000, algorithm.Count);
    }

    [Theory]
    [InlineData(false, 0, 10)]
    [InlineData(true, 0, 10)]
    [InlineData(false, 8, 8)]
    [InlineData(true, 8, 8)]
    [InlineData(true, 2, 2)]
    public void HistoryQueryLoadsFullyAndTheNextLiveResultsRestoreTheLimit(bool convert, int requested, int expected)
    {
        StaTest.Run(() =>
        {
            using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
            db.CodeFirst.InitTables<Row>();
            db.Insertable(Enumerable.Range(1, 10).Select(id => new Row { Id = id }).ToArray()).ExecuteCommand();
            var rows = new ResultHistoryCollection<Row>(row => row.Id, 3);
            int conversions = 0;
            GenericQueryBase query = convert
                ? new GenericQuery<Row, Row>(db, rows.QueryResults, row => { conversions++; return row; })
                : new GenericQuery<Row>(db, rows.QueryResults);
            query.QueryConfig.Count = requested;
            query.QueryDB();
            Assert.Equal(expected, rows.Count);
            Assert.Equal(Enumerable.Range(11 - expected, expected).Reverse(), rows.Select(row => row.Id));
            Assert.Equal(convert ? expected : 0, conversions);
            Assert.Equal(10, db.Queryable<Row>().Count());
            for (int id = 11; id <= 14; id++) rows.Add(new Row { Id = id });
            Assert.Equal(new[] { 12, 13, 14 }, rows.Select(row => row.Id));
            rows.MaxCount = 1;
            Assert.Equal(14, Assert.Single(rows).Id);
            query.QueryConfig.Count = 2;
            query.QueryDB();
            Assert.Equal(new[] { 10, 9 }, rows.Select(row => row.Id));
            rows.Add(new Row { Id = 14 });
            Assert.Equal(14, Assert.Single(rows).Id);
        });
    }

    [Fact]
    public void LargeSearchLoadsFullyAndContinuousTestingCanEvictItsRecords()
    {
        StaTest.Run(() =>
        {
            var rows = new ResultHistoryCollection<Row>(row => row.Id, 500);
            for (int id = 1; id <= 1000; id++) rows.QueryResults.Add(new Row { Id = id });
            Row selected = rows[0];
            ListView list = new() { ItemsSource = rows, SelectedItem = selected };
            for (int id = 1001; id <= 1600; id++) rows.Insert(0, new Row { Id = id });
            rows.Move(0, rows.Count - 1);
            rows.Add(new Row { Id = 1600 });
            rows.Add(new Row { Id = 1001 });
            Assert.Equal(500, rows.Count);
            Assert.Equal(Enumerable.Range(1101, 500), rows.Select(row => row.Id).Order());
            Assert.Null(list.SelectedItem);
            rows.MaxCount = 1;
            Assert.Equal(1600, Assert.Single(rows).Id);
            rows.Clear();
            Assert.Null(list.SelectedItem);
            Assert.False(rows.ContainsId(1));
            rows.Add(new Row { Id = 2 });
            Assert.Equal(2, Assert.Single(rows).Id);
            list.ItemsSource = null;
        });
    }

    [Fact]
    public void ResultMenusAndCommandsAreCreatedOnlyOnDemandAndSaveEntryIsNotDuplicated()
    {
        StaTest.Run(() =>
        {
            ViewResultAlg algorithm = new();
            ViewResultImage camera = new();
            Assert.Null(typeof(ViewResultAlg).GetField("_contextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(algorithm));
            Assert.Null(typeof(ViewResultImage).GetField("_contextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(camera));
            Assert.Null(typeof(ViewResultAlg).GetField("_exportCVCIECommand", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(algorithm));
            Assert.Null(typeof(ViewResultImage).GetField("_exportCVCIECommand", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(camera));
            Assert.Equal(3, camera.ContextMenu.Items.Count);
            Assert.Same(camera.ContextMenu, camera.ContextMenu);
            Assert.Same(camera.ExportCVCIECommand, ((MenuItem)camera.ContextMenu.Items[1]).Command);
            AlgorithmResultDataSaver.EnsureContextMenu(algorithm);
            AlgorithmResultDataSaver.EnsureContextMenu(algorithm);
            Assert.Single(algorithm.ContextMenu.Items.OfType<MenuItem>(), item => ReferenceEquals(item.Command, AlgorithmResultDataSaver.SaveCommand));
            Assert.Same(algorithm, algorithm.ContextMenu.Items.OfType<MenuItem>().Last().CommandParameter);
        });
    }

    [Fact]
    public void DeviceViewsApplyChangedLimitsAndReleaseHistoryWhenDisposed()
    {
        WpfTestHost.Invoke(() =>
        {
            IConfigService previousConfig = ConfigService.Instance;
            try
            {
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                using var device = new DeviceCamera(new SysResourceModel { Id = -2, Code = "history-offline", Value = "{}" });
                using var camera = new ViewCamera(device, true);
                using var algorithm = new AlgorithmView(null, true);
                for (int id = 1; id <= 4; id++)
                {
                    camera.ViewResults.Add(new ViewResultImage { Id = id });
                    algorithm.ViewResults.Add(new ViewResultAlg { Id = id });
                }
                ViewCamera.Config.MaxHistoryCount = 2;
                algorithm.Config.MaxHistoryCount = 2;
                Assert.Equal(new[] { 3, 4 }, camera.ViewResults.Select(row => row.Id));
                Assert.Equal(new[] { 3, 4 }, algorithm.ViewResults.Select(row => row.Id));
                Assert.IsType<ResultHistoryCollection<ViewResultImage>>(camera.ViewResults).QueryResults.Add(new ViewResultImage { Id = 1 });
                Assert.IsType<ResultHistoryCollection<ViewResultAlg>>(algorithm.ViewResults).QueryResults.Add(new ViewResultAlg { Id = 1 });
                ViewCamera.Config.MaxHistoryCount = 1;
                algorithm.Config.MaxHistoryCount = 1;
                Assert.Equal(4, Assert.Single(camera.ViewResults).Id);
                Assert.Equal(4, Assert.Single(algorithm.ViewResults).Id);
                camera.Dispose();
                algorithm.Dispose();
                Assert.Empty(camera.ViewResults);
                Assert.Empty(algorithm.ViewResults);
                ViewCamera.Config.MaxHistoryCount = 1;
                algorithm.Config.MaxHistoryCount = 1;
                Assert.Empty(camera.ViewResults);
                Assert.Empty(algorithm.ViewResults);
            }
            finally { ConfigService.SetInstance(previousConfig); }
        });
    }

    public sealed class Row : IEntity
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
    }
}
