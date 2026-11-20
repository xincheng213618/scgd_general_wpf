using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Calibration.Views;
using SqlSugar;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed partial class CalibrationResultOwnershipTests
{
    [Fact]
    public Task RecentHistoryFiltersByCalibrationOwnerBeforePagingAcrossDatabases() => WpfTestHost.Invoke(async () =>
    {
        using var fixture = new Fixture();
        string cameraCode = fixture.SeedHistory();
        var view = fixture.FirstView;
        ViewCalibration.Config.Count = 0;
        ViewCalibration.Config.OrderByType = OrderByType.Asc;

        await view.LoadRecentHistoryAsync().WaitAsync(Deadline);

        Assert.Equal(new[] { "original-first", "original-with-legacy-parameters", "original-calibrated-camera" },
            view.ViewResults.Select(row => row.ResultMsg));
        Assert.All(view.ViewResults, row => Assert.Same(fixture.Original, row.DatabaseEndpoint));
        Assert.Equal(cameraCode, view.ViewResults[2].DeviceCode);
        int calibratedId = view.ViewResults[2].Id;

        fixture.SwitchDatabase();
        ViewCalibration.Config.Count = 1;
        ViewCalibration.Config.OrderByType = OrderByType.Desc;
        await view.LoadRecentHistoryAsync().WaitAsync(Deadline);

        var replacement = Assert.Single(view.ViewResults);
        Assert.Equal(calibratedId, replacement.Id);
        Assert.Equal("replacement-calibrated-camera", replacement.ResultMsg);
        Assert.Same(fixture.Replacement, replacement.DatabaseEndpoint);
        Assert.Equal(1, fixture.OriginalReads);
        Assert.Equal(1, fixture.ReplacementReads);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RecentHistoryKeepsTheSelectedPreviewOnFaultOrRetirement(bool retireEndpoint) => WpfTestHost.Invoke(async () =>
    {
        using var fixture = new Fixture();
        fixture.SeedHistory();
        var view = fixture.FirstView;
        view.EnsureInitialized();
        var list = Assert.IsType<ListView>(view.FindName("listView1"));
        ViewCalibration.Config.Count = 1;
        ViewCalibration.Config.OrderByType = OrderByType.Asc;
        await view.LoadRecentHistoryAsync().WaitAsync(Deadline);
        var previous = Assert.Single(view.ViewResults);
        Assert.Equal("original-first", previous.ResultMsg);
        list.SelectedItem = previous;
        await view.ImageView.ImageOpenCompletion.WaitAsync(Deadline);
        var preview = Assert.IsAssignableFrom<BitmapSource>(view.ImageView.ViewBitmapSource);

        if (retireEndpoint)
        {
            var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.BeforeOriginalRead = () =>
            {
                Assert.False(view.Dispatcher.CheckAccess());
                reading.SetResult();
                release.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
            };
            Task retired = view.LoadRecentHistoryAsync();
            try
            {
                await reading.Task.WaitAsync(Deadline);
                fixture.SwitchDatabase();
                Assert.Same(previous, Assert.Single(view.ViewResults));
            }
            finally { release.TrySetResult(); }
            await retired.WaitAsync(Deadline);
        }
        else
        {
            fixture.BeforeOriginalRead = () => throw new IOException("Synthetic calibration history read failure.");
            await Assert.ThrowsAsync<IOException>(() => view.LoadRecentHistoryAsync().WaitAsync(Deadline));
        }

        Assert.Same(previous, Assert.Single(view.ViewResults));
        Assert.Same(previous, list.SelectedItem);
        Assert.Same(preview, view.ImageView.ViewBitmapSource);
        Assert.Equal(0, fixture.ReplacementReads);
    });

    [Fact]
    public Task AdvancedHistorySharesScopeAndKeepsNewerResultsWhenItFinishesLate() => WpfTestHost.Invoke(async () =>
    {
        using var fixture = new Fixture();
        string cameraCode = fixture.SeedHistory();
        var view = fixture.FirstView;
        var advanced = view.CreateHistoryQuery(out Action finishQuery);
        using var dialogDb = advanced.Db;
        try
        {
            Assert.True(advanced.IsReadOnly);
            Assert.False(advanced.DeleteAllCommand.CanExecute(null));
            Assert.False(advanced.TruncateTableCommand.CanExecute(null));
            int rowCount = fixture.CountHistoryRows("original");
            advanced.DeleteAllCommand.Execute(null);
            advanced.TruncateTableCommand.Execute(null);
            advanced.DeleteAll();
            advanced.TruncateTable();
            Assert.Equal(rowCount, fixture.CountHistoryRows("original"));

            advanced.QueryConfig.Count = 1;
            advanced.QueryConfig.OrderByType = OrderByType.Desc;
            advanced.AddPropertyInfo(typeof(MeasureResultImgModel).GetProperty(nameof(MeasureResultImgModel.DeviceCode))!);
            QueryCondition filter = Assert.Single(advanced.QueryConditions);
            filter.InputText = fixture.Second.Code;
            await advanced.QueryDBAsync(CancellationToken.None).WaitAsync(Deadline);
            Assert.Empty(view.ViewResults);

            filter.InputText = cameraCode;
            await advanced.QueryDBAsync(CancellationToken.None).WaitAsync(Deadline);
            Assert.Equal("original-calibrated-camera", Assert.Single(view.ViewResults).ResultMsg);

            var converted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            advanced.PreQuery += (_, _) =>
            {
                var convert = advanced.BatchConverter!;
                advanced.BatchConverter = rows =>
                {
                    Assert.False(view.Dispatcher.CheckAccess());
                    ViewResultImage[] prepared = convert(rows).ToArray();
                    Assert.Equal("original-calibrated-camera", Assert.Single(prepared).ResultMsg);
                    converted.SetResult();
                    release.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
                    return prepared;
                };
            };
            Task oldRequest = advanced.QueryDBAsync(CancellationToken.None);
            ViewResultImage latest;
            try
            {
                await converted.Task.WaitAsync(Deadline);
                ViewCalibration.Config.Count = 1;
                ViewCalibration.Config.OrderByType = OrderByType.Asc;
                await view.LoadRecentHistoryAsync().WaitAsync(Deadline);
                latest = Assert.Single(view.ViewResults);
                Assert.Equal("original-first", latest.ResultMsg);
            }
            finally { release.TrySetResult(); }

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => oldRequest.WaitAsync(Deadline));
            Assert.Same(latest, Assert.Single(view.ViewResults));
            Assert.Equal(0, fixture.ReplacementReads);
        }
        finally { finishQuery(); }
    });
}
