using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Monitoring;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

// Protect result correlation/units and event-driven read ownership across batch and window lifetime boundaries.
public sealed class LiveIvlTests
{
    private static readonly DateTime Start = new(2026, 10, 19, 12, 0, 0);
    private static MeasureBatchModel Batch(int id = 7) => new() { Id = id, Name = "Synthetic", CreateDate = Start, FlowStatus = FlowStatus.Runing };
    private static SMUResultModel Smu(int id, double seconds, float voltage, float current, SMUChannelType channel = SMUChannelType.A) => new()
    {
        Id = id, BatchId = 7, DeviceCode = "SMU-1", ChannelType = channel, ZIndex = 1,
        CreateDate = Start.AddSeconds(seconds), VResult = voltage, IResult = current, ResultCode = 0
    };
    private static LiveIvlSnapshot Empty(int id) => new(id, "Synthetic", FlowStatus.Ready, Start, [], 0, 0, false);

    [Fact]
    public void PartialBatchUsesReferencesAndKeepsRoundTripOrderAndChannelsSeparate()
    {
        var snapshot = LiveIvlData.Build(Batch(), [Smu(3, 3, 1, 30), Smu(1, 1, 1, 10), Smu(2, 2, 2, 20), Smu(4, 4, 9, 90, SMUChannelType.B)], [],
            [new() { Id = 10, Time = Start.AddSeconds(5), SmuId = 2, Poi = "Center", Luminance = 123 }],
            [new() { Id = 11, Time = Start.AddSeconds(6), SmuId = 1, Luminance = 456 }]);
        var electrical = snapshot.Points.Where(p => p.Series.StartsWith("SMU") && p.Series.Contains("/A/")).ToArray();
        Assert.Equal(new double?[] { 1, 2, 1 }, electrical.Select(p => p.Voltage));
        Assert.Equal(2, snapshot.Points.Where(p => p.Series.StartsWith("SMU")).Select(p => p.Series).Distinct().Count());
        Assert.Equal(20, snapshot.Points.Single(p => p.Luminance == 123).Current);
        Assert.Equal(10, snapshot.Points.Single(p => p.Luminance == 456).Current);
        Assert.Equal(0, snapshot.Unpaired);
    }

    [Fact]
    public void MissingOrAmbiguousLinksKeepLuminanceWithoutInventingElectricalValues()
    {
        var snapshot = LiveIvlData.Build(Batch(), [Smu(1, 1, 1, 10), Smu(2, 1, 2, 20)], [], [],
            [new() { Id = 1, Time = Start.AddSeconds(2), Luminance = 0 },
             new() { Id = 2, Time = Start.AddSeconds(2), SmuId = 999, Luminance = 20 },
             new() { Id = 3, Time = Start.AddSeconds(2), Voltage = 3, Current = 30, Luminance = 40 }]);
        Assert.Equal(2, snapshot.Unpaired);
        Assert.Null(snapshot.Points.Single(p => p.Luminance == 0).Voltage);
        Assert.Null(snapshot.Points.Single(p => p.Luminance == 20).Current);
        Assert.Equal(30, snapshot.Points.Single(p => p.Luminance == 40).Current);
    }

    [Fact]
    public void LegacyFallbackRequiresSingleSourceAndPrecedingUniqueTimestamp()
    {
        LiveIvlOpticalRow[] rows = [new() { Id = 1, Time = Start.AddSeconds(1.5), Luminance = 100 }];
        var snapshot = LiveIvlData.Build(Batch(), [Smu(1, 1, 1, 10), Smu(2, 2, 2, 20)], [], rows, []);
        Assert.Equal(10, snapshot.Points.Single(p => p.Luminance == 100).Current);
        snapshot = LiveIvlData.Build(Batch(), [Smu(1, 1, 1, 10), Smu(2, 1, 2, 20, SMUChannelType.B)], [], rows, []);
        Assert.Null(snapshot.Points.Single(p => p.Luminance == 100).Current);
    }

    [Fact]
    public void ScansConvertAmpsOnceAndRejectInvalidAndOtherBatchResults()
    {
        var failed = Smu(2, 2, 10, 10); failed.ResultCode = 1;
        var other = Smu(3, 3, 10, 10); other.BatchId = 99;
        var snapshot = LiveIvlData.Build(Batch(), [Smu(1, 1, 1, 2), failed, other],
            [new() { Id = 1, BatchId = 7, CreateDate = Start, VResult = "[1,2,1]", IResult = "[0.001,0.002,0.001]" },
             new() { Id = 2, BatchId = 7, VResult = "invalid", IResult = "[]" }], [], []);
        Assert.Equal(new double?[] { 1, 2, 1 }, snapshot.Points.Where(p => p.IsScan).Select(p => p.Current));
        Assert.All(snapshot.Points.Where(p => p.IsScan), p => Assert.Equal(Start, p.Time));
        Assert.Equal(2, snapshot.Points.Single(p => !p.IsScan).Current);
        Assert.Equal(1, snapshot.Invalid);
    }

    [Theory]
    [InlineData("batch")]
    [InlineData("close")]
    public async Task LateReadCannotOverwriteChangedOrClosedDisplay(string change)
    {
        var completion = new TaskCompletionSource<LiveIvlSnapshot>();
        int reads = 0;
        using var session = new LiveIvlSession((id, token) => { reads++; return completion.Task; });
        session.SelectBatch(7);
        var first = session.RefreshAsync();
        Assert.False(await session.RefreshAsync());
        if (change == "batch") session.SelectBatch(8);
        if (change == "close") session.Dispose();
        completion.SetResult(Empty(7));
        Assert.False(await first);
        Assert.Null(session.Snapshot);
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task FailedReadRetainsSnapshotAndRetriesOnNextRequest()
    {
        int reads = 0;
        using var session = new LiveIvlSession((id, token) => ++reads == 2
            ? Task.FromException<LiveIvlSnapshot>(new InvalidOperationException("synthetic disconnect"))
            : Task.FromResult(Empty(id)));
        session.SelectBatch(7);
        Assert.True(await session.RefreshAsync());
        var previous = session.Snapshot;
        Assert.True(await session.RefreshAsync());
        Assert.Same(previous, session.Snapshot);
        Assert.Contains("disconnect", session.Error);
        Assert.Equal(2, reads);
        Assert.True(await session.RefreshAsync());
        Assert.Null(session.Error);
    }

    [Fact]
    public async Task NodesCompletingDuringReadAreCoalescedWithoutLosingTheFinalResult()
    {
        var first = new TaskCompletionSource<LiveIvlSnapshot>();
        var second = new TaskCompletionSource<LiveIvlSnapshot>();
        var secondStarted = new TaskCompletionSource();
        int reads = 0, updates = 0;
        using var session = new LiveIvlSession((id, token) =>
        {
            if (++reads == 1) return first.Task;
            secondStarted.SetResult();
            return second.Task;
        });
        session.Updated += (_, _) => updates++;
        session.SelectBatch(7);
        var pending = session.RefreshAsync();
        Assert.False(await session.RefreshAsync());
        Assert.False(await session.RefreshAsync());
        first.SetResult(Empty(7));
        await secondStarted.Task;
        Assert.Equal(1, updates); // The first snapshot is already visible while more nodes keep completing.
        second.SetResult(Empty(7) with { Name = "Final node" });
        Assert.True(await pending);
        Assert.Equal(2, reads);
        Assert.Equal(2, updates);
        Assert.Equal("Final node", session.Snapshot!.Name);
    }

    [Fact]
    public async Task NewBatchIsReadAfterAnOlderInFlightReadFinishes()
    {
        var old = new TaskCompletionSource<LiveIvlSnapshot>();
        var requests = new List<int>();
        using var session = new LiveIvlSession((id, token) =>
        {
            requests.Add(id);
            return id == 7 ? old.Task : Task.FromResult(Empty(id));
        });
        session.SelectBatch(7);
        var pending = session.RefreshAsync();
        session.SelectBatch(8);
        Assert.False(await session.RefreshAsync());
        old.SetResult(Empty(7));
        Assert.True(await pending);
        Assert.Equal(new[] { 7, 8 }, requests);
        Assert.Equal(8, session.Snapshot!.BatchId);
    }

    [Fact]
    public void ToolWindowLoadsLayoutAndCanCloseWithoutStartingHardwareOrDatabase()
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new LiveIvlWindow();
            Assert.Equal(7, ((ComboBox)window.FindName("Mode")).Items.Count);
            Assert.Equal(2, ((System.Windows.Controls.Grid)window.Content).ColumnDefinitions.Count);
            Assert.Equal(ColorVision.UI.Menus.MenuItemConstants.Tool, new LiveIvlMenuItem().OwnerGuid);
            Assert.NotNull(window.FindName("FlowSelection"));
            Assert.Equal("View.RunFlowCommand", ((Button)window.FindName("RunButton")).GetBindingExpression(Button.CommandProperty)!.ParentBinding.Path.Path);
            Assert.Equal("View.StopFlowCommand", ((Button)window.FindName("StopButton")).GetBindingExpression(Button.CommandProperty)!.ParentBinding.Path.Path);
            Assert.Null(window.FindName("Interval"));
            window.Close();
        });
    }
}
