using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.Sensor.Local;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using MQTTMessageLib.Sensor;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.UI.Tests;

// These regressions protect framing, cancellation and persisted template compatibility.
public sealed class LocalSensorSessionTests
{
    [Fact]
    public async Task DiagnosticWindowViewsExistingRecordsAndClosingOrClearingDoesNotControlConnection()
    {
        var transport = new FakeTransport { Reply = _ => "{\"ok\":true}" };
        using var session = new LocalSensorSession(_ => transport);
        await session.OpenAsync(Config());
        const string request = "{\"event\":\"query\"}";
        await session.ExecuteAsync([new() { Name = "Query", Request = request, Response = "{\"ok\":true}" }]);
        WpfTestHost.Invoke(() =>
        {
            var previous = Application.Current.Resources;
            SensorDiagnosticsWindow? window = null;
            try
            {
                var resources = new ResourceDictionary();
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                    resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
                Application.Current.Resources = resources;
                window = new SensorDiagnosticsWindow("Sensor", session, () => "TCP test") { Left = -16000, Top = -16000, ShowInTaskbar = false, ShowActivated = false };
                window.Show(); window.UpdateLayout();
                var grid = (DataGrid)window.FindName("MessageGrid");
                Assert.Equal(2, grid.Items.Count);
                grid.SelectedIndex = 0;
                var detail = (TextBox)window.FindName("DetailText");
                Assert.Contains(Environment.NewLine, detail.Text);
                ((CheckBox)window.FindName("FormatJson")).IsChecked = false;
                Assert.Equal(request, detail.Text);
                var search = (TextBox)window.FindName("SearchBox");
                search.Text = "no-such-record";
                Assert.Empty(grid.Items);
                search.Clear();
                ((ComboBox)window.FindName("DirectionFilter")).SelectedIndex = 2;
                Assert.Single(grid.Items);
                Assert.Equal(LocalSensorDiagnosticKind.Received, ((LocalSensorDiagnosticEntry)grid.Items[0]).Kind);
                const string replyHex = "50472C494D4147452C77302E706E672C454E440D0A";
                session.Diagnostics.AddTraffic(LocalSensorDiagnosticKind.Received, "test",
                    new() { Name = "Image", CmdType = SensorCmdType.Hex }, 2, 1, Convert.FromHexString(replyHex));
                session.Diagnostics.AddTraffic(LocalSensorDiagnosticKind.Received, "test",
                    new() { Name = "Unicode", CmdType = SensorCmdType.UTF8 }, 3, 1, Encoding.UTF8.GetBytes("完成\r\n\t"));
                window.RefreshRecords();
                search.Text = "PG,IMAGE";
                var echo = Assert.Single(grid.Items.Cast<LocalSensorDiagnosticEntry>());
                grid.SelectedItem = echo;
                Assert.Equal("PG,IMAGE,w0.png,END[CR][LF]", echo.Preview);
                Assert.Equal(echo.Preview, detail.Text);
                Assert.Equal(replyHex, echo.Text);
                Assert.Equal(replyHex, ((TextBox)window.FindName("DetailOriginalText")).Text);
                Assert.Equal("50 47 2C 49 4D 41 47 45 2C 77 30 2E 70 6E 67 2C 45 4E 44 0D 0A", ((TextBox)window.FindName("DetailHex")).Text);
                search.Text = "[TAB]";
                echo = Assert.Single(grid.Items.Cast<LocalSensorDiagnosticEntry>());
                grid.SelectedItem = echo;
                Assert.Equal("完成[CR][LF][TAB]", detail.Text);
                Assert.Equal("完成\r\n\t", echo.Text);
                search.Clear();
                ((TabControl)window.FindName("RecordTabs")).SelectedIndex = 1;
                Assert.All(grid.Items.Cast<LocalSensorDiagnosticEntry>(), entry => Assert.Equal(LocalSensorDiagnosticKind.Log, entry.Kind));
                Assert.NotEmpty(grid.Items);
                ((Button)window.FindName("ClearHistoryButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Empty(grid.Items);
                Assert.True(session.IsOpen);
                window.Close();
                Assert.True(session.IsOpen);
                Assert.False(transport.Disposed);
                session.Diagnostics.AddLog("test", "关闭窗口后", "仍然采集");
                window = new SensorDiagnosticsWindow("Sensor", session, () => "TCP test") { Left = -16000, Top = -16000, ShowInTaskbar = false, ShowActivated = false };
                window.Show();
                ((TabControl)window.FindName("RecordTabs")).SelectedIndex = 1;
                Assert.Single(((DataGrid)window.FindName("MessageGrid")).Items);
                Assert.Equal(1, transport.Opens);
                Assert.Equal(1, transport.Writes);
            }
            finally { window?.Close(); Application.Current.Resources = previous; }
        });
    }

    [Fact]
    public async Task DiagnosticsRetainEveryFragmentAndRetryWithoutChangingResponseMatching()
    {
        var transport = new FakeTransport { ReplyChunks = attempt => attempt == 1 ? ["N"u8.ToArray(), "O"u8.ToArray()] : ["O"u8.ToArray(), "K"u8.ToArray()] };
        using var session = new LocalSensorSession(_ => transport);
        await session.OpenAsync(Config());
        var result = (await session.ExecuteAsync([new() { Name = "Query", Request = "Q", Response = "OK", RetryCount = 2 }])).Single();
        Assert.Equal("OK", result.ResponseText);
        Assert.Equal(2, result.Attempts);
        var history = session.Diagnostics.GetSnapshot(-1, out _)!;
        var traffic = history.Where(entry => entry.Kind is LocalSensorDiagnosticKind.Sent or LocalSensorDiagnosticKind.Received).ToArray();
        Assert.Equal(new[] { "51", "4E", "4F", "51", "4F", "4B" }, traffic.Select(entry => entry.Hex));
        Assert.All(traffic, entry => { Assert.Equal("Query", entry.CommandName); Assert.Equal(1, entry.CommandId); });
        Assert.Equal(new[] { 1, 1, 1, 2, 2, 2 }, traffic.Select(entry => entry.Attempt));
        Assert.Contains(history, entry => entry.EventName == "回包不匹配，重试");
        Assert.Contains(history, entry => entry.EventName == "执行完成" && entry.Text.Contains("OK"));
        await session.CloseAsync();
        Assert.Contains(session.Diagnostics.GetSnapshot(-1, out _)!, entry => entry.EventName == "连接关闭");
    }

    [Fact]
    public async Task DiagnosticsCaptureUnsolicitedBytesWithoutAnOpenWindowOrPendingCommand()
    {
        var transport = new FakeTransport();
        using var session = new LocalSensorSession(_ => transport);
        await session.OpenAsync(Config());
        transport.QueueReceived([0x00, 0xFF]);
        // Starting the next read proves the previous chunk passed through the session's receive path.
        await transport.SecondReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var received = Assert.Single(session.Diagnostics.GetSnapshot(-1, out _)!, entry => entry.Kind == LocalSensorDiagnosticKind.Received);
        Assert.Equal("00FF", received.Hex);
        Assert.Equal("[NUL][FF]", received.DisplayText);
        Assert.Equal("Hex", received.Encoding);
        Assert.Equal(0, received.CommandId);
        Assert.True(session.IsOpen);
    }

    [Fact]
    public void DiagnosticRetentionIsBoundedAndClearInvalidatesSnapshots()
    {
        var diagnostics = new LocalSensorDiagnostics();
        byte[] payload = new byte[LocalSensorDiagnostics.MaxPayloadBytes + 1];
        payload[0] = 0x41;
        diagnostics.AddTraffic(LocalSensorDiagnosticKind.Received, "test", null, 0, 0, payload);
        payload[0] = 0xFF;
        var entry = Assert.Single(diagnostics.GetSnapshot(-1, out long revision)!);
        Assert.Equal("41", entry.Hex[..2]);
        Assert.True(entry.IsTruncated);
        Assert.Equal(payload.Length, entry.ByteCount);
        Assert.Equal(LocalSensorDiagnostics.MaxPayloadBytes, entry.CapturedByteCount);
        Assert.Null(diagnostics.GetSnapshot(revision, out _));
        for (int index = 0; index < LocalSensorDiagnostics.MaxEntries + 1; index++) diagnostics.AddLog("test", "log", index.ToString());
        var retained = diagnostics.GetSnapshot(revision, out revision)!;
        Assert.Equal(LocalSensorDiagnostics.MaxEntries, retained.Length);
        Assert.Equal("1", retained[0].Text);
        diagnostics.Clear();
        Assert.Empty(diagnostics.GetSnapshot(revision, out _)!);
    }

    [Fact]
    public async Task FramingWaitsForCompleteBytes()
    {
        var command = new LocalSensorCommand { Request = "Q", Response = "OK" };
        command.Validate();
        var exchange = new LocalSensorSession.Exchange(command);
        byte[] bytes = Encoding.ASCII.GetBytes("OK");
        foreach (byte value in bytes[..^1]) { exchange.Receive([value]); Assert.False(exchange.Completion.Task.IsCompleted); }
        exchange.Receive(bytes.AsSpan(bytes.Length - 1));
        Assert.Equal(bytes, await exchange.Completion.Task);
        Assert.True(exchange.Matches(bytes));
    }

    [Fact]
    public async Task Utf8AndHexMatchBytesAcrossCharacterBoundaries()
    {
        foreach (var command in new[] {
            new LocalSensorCommand { CmdType = SensorCmdType.UTF8, Request = "查询", Response = "完成" },
            new LocalSensorCommand { CmdType = SensorCmdType.Hex, Request = "0x01 02", Response = "AA,00,FF" } })
        {
            byte[] response = command.Encode(command.Response);
            var exchange = new LocalSensorSession.Exchange(command);
            exchange.Receive(response.AsSpan(0, 1));
            Assert.False(exchange.Completion.Task.IsCompleted);
            exchange.Receive(response.AsSpan(1));
            Assert.True(exchange.Matches(await exchange.Completion.Task));
        }
    }

    [Fact]
    public async Task ClosingCancelsOpeningAndDoesNotReopen()
    {
        var transport = new FakeTransport { BlockOpen = true };
        using var session = new LocalSensorSession(_ => transport);
        Task opening = session.OpenAsync(Config());
        await transport.OpenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task queuedOpen = session.OpenAsync(Config());
        await session.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queuedOpen);
        Assert.Equal(1, transport.Opens);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync([new() { Request = "Q" }]));
    }

    [Fact]
    public async Task LaterCloseCancelsReopenWaitingForOldConnection()
    {
        var unwind = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new FakeTransport { BlockOpen = true, OpeningUnwind = unwind };
        using var session = new LocalSensorSession(_ => transport);
        var config = Config();
        Task opening = session.OpenAsync(config);
        await transport.OpenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task reopening = session.ReopenAsync(config);
        Task closing = session.CloseAsync();
        unwind.TrySetResult();
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reopening);
        Assert.Equal(1, transport.Opens);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
    }

    [Fact]
    public async Task ClosingCancelsActiveAndQueuedCommands()
    {
        var transport = new FakeTransport();
        using var session = new LocalSensorSession(_ => transport);
        await session.OpenAsync(Config());
        Task first = session.ExecuteAsync([new() { Request = "Q", Response = "OK", Timeout = 10000 }]);
        await transport.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task queued = session.ExecuteAsync([new() { Request = "NEXT" }]);
        await session.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.Equal(1, transport.Writes);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
    }

    [Fact]
    public async Task CallerCancellationClosesActiveExchange()
    {
        var transport = new FakeTransport();
        using var session = new LocalSensorSession(_ => transport);
        using var stop = new CancellationTokenSource();
        await session.OpenAsync(Config());
        Task run = session.ExecuteAsync([new() { Request = "Q", Response = "OK", Timeout = 10000 }], stop.Token);
        await transport.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(transport.Disposed);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeviceLifecycleEntersSessionBeforeReturningToCaller(bool closeFirst)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        ColorVision.UI.IConfigService? previousConfig = null;
        DeviceSensor? device = null;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            WpfTestHost.Invoke(() =>
            {
                previousConfig = ConfigService.Instance;
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                device = new DeviceSensor(new() { Id = -2, Code = "sensor-lifecycle-" + Guid.NewGuid(), Value = Newtonsoft.Json.JsonConvert.SerializeObject(new ConfigSensor { IsNet = true, Addr = "127.0.0.1", Port = port }) });
                var originalDisplayConfig = device.DisplayConfig;
                device.Config.Code += "-renamed";
                Assert.Same(originalDisplayConfig, device.DisplayConfig);
                device.DisplayConfig.UseLocalSensor = false;
                Assert.False(device.SensorBackend.OpensLocally);
                device.DisplayConfig.UseLocalSensor = true;
                device.DService.Open();
                Assert.NotEqual(DeviceStatusType.Closed, device.LocalSession.Status);
                if (!closeFirst)
                {
                    device.Dispose();
                    Assert.False(device.SensorBackend.LocalOwned);
                    Assert.Equal(DeviceStatusType.Closed, device.LocalSession.Status);
                    closed.TrySetResult();
                    return;
                }
                var record = device.DService.Close();
                Assert.Equal(DeviceStatusType.Closed, device.LocalSession.Status);
                void Complete(object? sender, ColorVision.Engine.Messages.MsgRecordState state)
                {
                    if (state == ColorVision.Engine.Messages.MsgRecordState.Success) closed.TrySetResult();
                    else if (state is ColorVision.Engine.Messages.MsgRecordState.Fail or ColorVision.Engine.Messages.MsgRecordState.Timeout)
                        closed.TrySetException(new InvalidOperationException(record.MsgReturn?.Message));
                }
                record.MsgRecordStateChanged += Complete;
                Complete(record, record.MsgRecordState);
            });
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            WpfTestHost.Invoke(() => { device?.Dispose(); if (previousConfig != null) ConfigService.SetInstance(previousConfig); });
        }
    }

    [Fact]
    public async Task PartialTimeoutClosesConnectionBeforeNextCommand()
    {
        var transport = new FakeTransport { Reply = _ => "O" };
        using var session = new LocalSensorSession(_ => transport);
        await session.OpenAsync(Config());
        var failure = await Assert.ThrowsAsync<TimeoutException>(() => session.ExecuteAsync([new() { Name = "query", Request = "Q", Response = "OK", Timeout = 100 }]));
        Assert.Contains("query", failure.Message);
        Assert.Contains("O", failure.Message);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
        var history = session.Diagnostics.GetSnapshot(-1, out _)!;
        Assert.Contains(history, entry => entry.Kind == LocalSensorDiagnosticKind.Received && entry.Hex == "4F");
        Assert.Contains(history, entry => entry.Kind == LocalSensorDiagnosticKind.Error && entry.Text.Contains(failure.Message));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync([new() { Request = "NEXT" }]));
        Assert.Equal(1, transport.Writes);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public async Task RetryUsesExistingTemplateAttemptCount(int retries, int writes)
    {
        var transport = new FakeTransport { Reply = attempt => attempt == 1 ? "NO" : "OK" };
        using var session = new LocalSensorSession(_ => transport);
        await session.OpenAsync(Config());
        Task<IReadOnlyList<LocalSensorCommandResult>> run = session.ExecuteAsync([LocalSensorCommand.FromTemplate($"Q,OK,Ascii,1000/0,{retries}")]);
        if (retries == 2) Assert.Equal(2, (await run).Single().Attempts);
        else await Assert.ThrowsAsync<InvalidDataException>(() => run);
        Assert.Equal(writes, transport.Writes);
    }

    [Fact]
    public async Task DuplicateEndpointCannotOpenUntilOwnerCloses()
    {
        using var first = new LocalSensorSession(_ => new FakeTransport());
        using var second = new LocalSensorSession(_ => new FakeTransport());
        var config = Config();
        await first.OpenAsync(config);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.OpenAsync(config));
        await first.CloseAsync();
        await second.OpenAsync(config);
        Assert.True(second.IsOpen);
    }

    [Fact]
    public async Task FailingStatusSubscriberCannotPreventClosingConnection()
    {
        var transport = new FakeTransport();
        using var session = new LocalSensorSession(_ => transport);
        session.StatusChanged += _ => throw new InvalidOperationException("subscriber error");
        await session.OpenAsync(Config());
        await session.CloseAsync();
        Assert.True(transport.Disposed);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
    }

    [Fact]
    public async Task TcpTransportReceivesReplyAndDetectsDisconnect()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var config = new LocalSensorConnectionConfig { IsNet = true, Addr = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port };
        using var session = new LocalSensorSession();
        await session.OpenAsync(config);
        using var peer = await listener.AcceptTcpClientAsync();
        var run = session.ExecuteAsync([new() { Request = "Q", Response = "OK" }]);
        byte[] request = new byte[1];
        await peer.GetStream().ReadExactlyAsync(request).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal((byte)'Q', request[0]);
        await peer.GetStream().WriteAsync("O"u8.ToArray());
        await peer.GetStream().WriteAsync("K"u8.ToArray());
        Assert.Equal("OK", (await run.WaitAsync(TimeSpan.FromSeconds(5))).Single().ResponseText);
        Task next = session.ExecuteAsync([new() { Request = "N", Response = "OK" }]);
        await peer.GetStream().ReadExactlyAsync(request).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        peer.Dispose();
        await Assert.ThrowsAnyAsync<IOException>(() => next.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(DeviceStatusType.Closed, session.Status);
    }

    [Fact]
    public void ExistingTemplateFieldsAndValueBArePreserved()
    {
        const string stored = "Q,OK,WITH,COMMA,Ascii,1000/20,2";
        var detail = new ColorVision.Engine.ModDetailModel { ValueA = stored, ValueB = "historical backup" };
        var editor = new SensorCommand(detail);
        var old = LocalSensorCommand.FromTemplate(detail.ValueA);
        Assert.Equal("OK,WITH,COMMA", old.Response);
        Assert.Equal(20, old.Delay);
        Assert.Equal(2, old.RetryCount);
        Assert.Equal(editor.Request, old.Request);
        Assert.Equal(editor.Response, old.Response);
        Assert.Equal(stored, detail.ValueA);
        editor.Timeout = 1500;
        Assert.Equal("Q,OK,WITH,COMMA,Ascii,1500/20,2", detail.ValueA);
        Assert.Equal("historical backup", detail.ValueB);
    }

    [Theory]
    [InlineData("01", SensorCmdType.Hex, "", 5000, 1000)]
    [InlineData("01,02", SensorCmdType.Hex, "02", 5000, 1000)]
    [InlineData("Q,OK,Ascii", SensorCmdType.Ascii, "OK", 5000, 1000)]
    [InlineData("Q,OK,Ascii,500/10", SensorCmdType.Ascii, "OK", 500, 10)]
    public void HistoricalShortTemplatesKeepServiceDefaults(string value, SensorCmdType encoding, string response, int timeout, int delay)
    {
        var command = LocalSensorCommand.FromTemplate(value);
        Assert.Equal(encoding, command.CmdType);
        Assert.Equal(response, command.Response);
        Assert.Equal(timeout, command.Timeout);
        Assert.Equal(delay, command.Delay);
        Assert.Equal(3, command.RetryCount);
        command.Validate();
    }

    private static LocalSensorConnectionConfig Config() => new() { IsNet = true, Addr = "fake-" + Guid.NewGuid(), Port = 1234 };

    [Fact]
    public void LocalOptionsAreStoredOnlyInDisplayConfiguration()
    {
        var settings = new DisplaySensorConfig { UseLocalSensor = true, ConnectTimeout = 2500, DataBits = 7,
            Parity = System.IO.Ports.Parity.Even, StopBits = System.IO.Ports.StopBits.Two, DtrEnable = true, RtsEnable = true };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
        var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<DisplaySensorConfig>(json)!;
        Assert.True(loaded.UseLocalSensor);
        Assert.Equal(2500, loaded.ConnectTimeout);
        Assert.Equal(7, loaded.DataBits);
        Assert.Equal(settings.Parity, loaded.Parity);
        Assert.Equal(settings.StopBits, loaded.StopBits);
        Assert.True(loaded.DtrEnable && loaded.RtsEnable);
        var databaseConfig = Newtonsoft.Json.Linq.JObject.FromObject(new ConfigSensor());
        foreach (string name in new[] { "UseLocalSensor", "ConnectTimeout", "DataBits", "Parity", "StopBits", "DtrEnable", "RtsEnable" })
            Assert.Null(databaseConfig[name]);
    }

    [Fact]
    public void KnownServiceOwnershipSurvivesOfflineAndModeChangesWaitForClose()
    {
        var backend = new SensorBackendState(false);
        backend.ObserveService(DeviceStatusType.Opened);
        backend.ObserveService(DeviceStatusType.OffLine);
        backend.SetPreference(true);
        Assert.False(backend.OpensLocally);
        Assert.Throws<InvalidOperationException>(backend.EnsureLocalAvailable);
        backend.ObserveService(DeviceStatusType.Closed);
        Assert.True(backend.OpensLocally);
        backend.SetLocalStatus(DeviceStatusType.Opening);
        backend.SetPreference(false);
        Assert.True(backend.OpensLocally);
        Assert.Throws<InvalidOperationException>(backend.EnsureServiceAvailable);
        backend.SetLocalStatus(DeviceStatusType.Closed);
        Assert.False(backend.OpensLocally);
        backend.EnsureServiceAvailable();
    }

    [Fact]
    public void InvalidHistoricalTemplateRemainsEditableWithoutRewritingSource()
    {
        const string damaged = "Q,OK,InvalidEncoding,1000/0,0";
        var detail = new ColorVision.Engine.ModDetailModel { ValueA = damaged, ValueB = "backup" };
        var command = new SensorCommand(detail);
        Assert.Equal(damaged, detail.ValueA);
        Assert.Throws<InvalidDataException>(() => LocalSensorCommand.FromTemplate(detail.ValueA));
        command.Request = "Q";
        Assert.Equal("Q", LocalSensorCommand.FromTemplate(detail.ValueA).Request);
        Assert.Equal("backup", detail.ValueB);
    }
    private sealed class FakeTransport : ILocalSensorTransport
    {
        private readonly Channel<byte[]> received = Channel.CreateUnbounded<byte[]>();
        public bool BlockOpen { get; init; }
        public TaskCompletionSource? OpeningUnwind { get; init; }
        public Func<int, string>? Reply { get; init; }
        public Func<int, byte[][]>? ReplyChunks { get; init; }
        public int Opens, Writes;
        private int reads;
        public bool Disposed { get; private set; }
        public TaskCompletionSource OpenStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void QueueReceived(byte[] bytes) => received.Writer.TryWrite(bytes);
        public async Task OpenAsync(LocalSensorConnectionConfig config, CancellationToken token)
        {
            Opens++; OpenStarted.TrySetResult();
            if (BlockOpen)
            {
                try { await Task.Delay(Timeout.Infinite, token); }
                finally { if (OpeningUnwind != null) await OpeningUnwind.Task; }
            }
        }
        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        { if (++reads == 2) SecondReadStarted.TrySetResult(); byte[] bytes = await received.Reader.ReadAsync(token); bytes.CopyTo(buffer); return bytes.Length; }
        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Writes++;
            if (Reply != null) received.Writer.TryWrite(Encoding.ASCII.GetBytes(Reply(Writes)));
            if (ReplyChunks != null) foreach (byte[] chunk in ReplyChunks(Writes)) received.Writer.TryWrite(chunk);
            WriteStarted.TrySetResult(); return ValueTask.CompletedTask;
        }
        public void Dispose() { Disposed = true; received.Writer.TryComplete(); }
    }
}
