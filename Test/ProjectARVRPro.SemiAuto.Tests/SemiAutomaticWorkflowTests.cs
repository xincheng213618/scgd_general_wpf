using ProjectARVRPro.SemiAuto.Automation;
using ProjectARVRPro.SemiAuto.GECS;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ProjectARVRPro.SemiAuto.Tests;

public class SemiAutomaticWorkflowTests
{
    [Fact]
    public async Task SuccessfulPgResponseTriggersArvrConfirmationAfterPgCompletes()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var events = new ConcurrentQueue<string>();
        Task server = RunPgServerAsync(listener, events, "PG,01,PATTERN,INDEX,7", false);
        using var client = new GecsClient();
        var workflow = new SemiAutomaticWorkflow(client);
        IntegrationProfile profile = CreateProfile(port);
        PgActionMapping mapping = profile.Mappings.Single();

        SemiAutomaticWorkflowResult result = await workflow.ExecuteAsync(
            profile,
            mapping,
            "3",
            "SN-E2E-001",
            true,
            () =>
            {
                events.Enqueue("arvr-confirmed");
                return Task.FromResult(true);
            });

        await server;
        Assert.True(result.PgSucceeded);
        Assert.True(result.ArvrConfirmed);
        Assert.Equal("PG,01,PATTERN,INDEX,7", result.CommandText);
        string[] eventSequence = events.ToArray();
        Assert.Equal(3, eventSequence.Length);
        Assert.Equal("pg-command-received", eventSequence[0]);
        Assert.Equal("pg-success-sent", eventSequence[1]);
        Assert.Equal("arvr-confirmed", eventSequence[2]);
    }

    [Fact]
    public async Task FailedPgResponseNeverTriggersArvrConfirmation()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var events = new ConcurrentQueue<string>();
        Task server = RunPgServerAsync(listener, events, "PG,01,PATTERN,INDEX,7", true);
        using var client = new GecsClient();
        var workflow = new SemiAutomaticWorkflow(client);
        IntegrationProfile profile = CreateProfile(port);
        int confirmationCount = 0;

        SemiAutomaticWorkflowResult result = await workflow.ExecuteAsync(
            profile,
            profile.Mappings.Single(),
            "3",
            "SN-E2E-002",
            true,
            () =>
            {
                confirmationCount++;
                return Task.FromResult(true);
            });

        await server;
        Assert.False(result.PgSucceeded);
        Assert.False(result.ArvrConfirmed);
        Assert.Equal(0, confirmationCount);
        Assert.Contains("END,NG", result.ErrorMessage);
    }

    [Fact]
    public void ProfileFindsEnabledMappingAndExpandsConfiguredVariables()
    {
        IntegrationProfile profile = CreateProfile(40009);

        PgActionMapping mapping = profile.FindMapping("switchpg", "3");
        string command = profile.ExpandCommand(mapping, "3", "SN-ABC");

        Assert.NotNull(mapping);
        Assert.Equal("PG,01,PATTERN,INDEX,7", command);
        Assert.Null(profile.FindMapping("SwitchPG", "4"));
    }

    [Fact]
    public async Task PowerSequenceSendsPowerOnWithSerialNumberThenPowerOff()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task server = RunPowerServerAsync(listener, "SN-E2E-POWER");
        using var client = new GecsClient();
        var workflow = new SemiAutomaticWorkflow(client);
        IntegrationProfile profile = CreateProfile(port);

        GecsCommandResult powerOn = await workflow.SetPowerAsync(profile, true, "SN-E2E-POWER");
        GecsCommandResult powerOff = await workflow.SetPowerAsync(profile, false, string.Empty);

        await server;
        Assert.True(powerOn.IsSuccess);
        Assert.True(powerOff.IsSuccess);
        Assert.Contains("POWER,ON,SN-E2E-POWER,END,OK", powerOn.ResponseText);
        Assert.Contains("POWER,OFF,END,OK", powerOff.ResponseText);
    }

    private static IntegrationProfile CreateProfile(int port)
    {
        return new IntegrationProfile
        {
            PgHost = "127.0.0.1",
            PgPort = port,
            NetworkNumber = 255,
            Channel = "01",
            PgResponseTimeoutSeconds = 5,
            HeartbeatSeconds = 0,
            Mappings = new List<PgActionMapping>
            {
                new PgActionMapping
                {
                    Enabled = true,
                    EventName = "SwitchPG",
                    ArvrTestType = "3",
                    Name = "E2E mapping",
                    CommandTemplate = "PG,{channel},PATTERN,INDEX,7",
                    SuccessContains = ",PATTERN,INDEX,END,OK"
                }
            }
        };
    }

    private static async Task RunPgServerAsync(TcpListener listener, ConcurrentQueue<string> events, string expectedCommand, bool fail)
    {
        using TcpClient serverClient = await listener.AcceptTcpClientAsync();
        using NetworkStream stream = serverClient.GetStream();
        var reader = new GecsFrameReader(stream);
        GecsFrame request = await reader.ReadFrameAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(request);
        Assert.Equal(expectedCommand, request.MessageText);
        events.Enqueue("pg-command-received");

        await WriteFrameAsync(stream, "PG,01,PATTERN,processing ...");
        string finalResponse = expectedCommand + (fail ? ",END,NG,E101" : ",END,OK");
        await WriteFrameAsync(stream, finalResponse);
        events.Enqueue(fail ? "pg-failure-sent" : "pg-success-sent");
    }

    private static async Task RunPowerServerAsync(TcpListener listener, string serialNumber)
    {
        using TcpClient serverClient = await listener.AcceptTcpClientAsync();
        using NetworkStream stream = serverClient.GetStream();
        var reader = new GecsFrameReader(stream);

        GecsFrame powerOn = await reader.ReadFrameAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("PG,01,POWER,ON," + serialNumber, powerOn.MessageText);
        await WriteFrameAsync(stream, "PG,01,POWER,ON," + serialNumber + ",END,OK");

        GecsFrame powerOff = await reader.ReadFrameAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("PG,01,POWER,OFF", powerOff.MessageText);
        await WriteFrameAsync(stream, "PG,01,POWER,OFF,END,OK");
    }

    private static async Task WriteFrameAsync(NetworkStream stream, string text)
    {
        byte[] packet = GecsPacketCodec.BuildPacket(text, 0xFF);
        await stream.WriteAsync(packet);
        await stream.FlushAsync();
    }
}
