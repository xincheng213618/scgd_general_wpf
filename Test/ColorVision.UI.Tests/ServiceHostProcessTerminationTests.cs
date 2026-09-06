using ColorVisionServiceHost;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using UiServiceHostClient = ColorVision.UI.ServiceHost.ColorVisionServiceHostClient;

namespace ColorVision.UI.Tests;

public sealed class ServiceHostProcessTerminationTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ColorVision-ProcessApi-").FullName;
    private readonly List<Process> _processes = [];
    private readonly ServiceHostCommandHandler _handler = new();
    private readonly ServiceHostRequestContext _context = new() { ProcessId = Environment.ProcessId, UserSid = "test-caller", ProcessSha256 = "test-hash" };

    [Theory]
    [InlineData("ColorVision.exe")]
    [InlineData("RegWindowsService.exe")]
    [InlineData("CVMainWindowsService_x64.exe")]
    [InlineData("CVMainWindowsService_dev.exe")]
    [InlineData("mysqld.exe")]
    [InlineData("mysql.exe")]
    [InlineData("mosquitto.exe")]
    public void AllowlistedProgramTerminatesOnlyTheRequestedProcess(string fileName)
    {
        Process target = StartProbe(fileName);
        Process other = StartProbe(fileName);
        ServiceHostRequest request = CreateRequest(target);

        ServiceHostResponse response = SendAuthorized(request);

        Assert.True(response.Success, response.ToDisplayText());
        Assert.True(target.HasExited);
        Assert.False(other.HasExited);
    }

    [Theory]
    [InlineData("birth", "process_identity_mismatch")]
    [InlineData("path", "process_identity_mismatch")]
    [InlineData("expired", "process_request_expired")]
    [InlineData("unbounded", "process_request_expired")]
    public void InvalidIdentityOrExpiredRequestPreservesTheProcess(string change, string error)
    {
        Process target = StartProbe("ColorVision.exe");
        ServiceHostRequest request = CreateRequest(target);
        request.Data![change switch
        {
            "birth" => "startTimeUtcTicks",
            "path" => "executablePath",
            _ => "notAfterUtcTicks",
        }] = change switch
        {
            "birth" => new JValue(target.StartTime.ToUniversalTime().AddSeconds(-1).Ticks),
            "path" => new JValue(Path.Combine(_directory, "OtherInstallation", "ColorVision.exe")),
            "expired" => new JValue(DateTime.UtcNow.AddSeconds(-1).Ticks),
            _ => new JValue(DateTime.UtcNow.AddMinutes(5).Ticks),
        };

        ServiceHostResponse response = SendAuthorized(request);

        Assert.False(response.Success);
        Assert.Equal(error, response.Message);
        Assert.False(target.HasExited);
    }

    [Fact]
    public void MissingBrokerTicketCannotTerminateAProcess()
    {
        Process target = StartProbe("Worker.exe");
        ServiceHostResponse response = _handler.Handle(CreateRequest(target), _context);
        Assert.False(response.Success);
        Assert.Equal("broker_ticket_required", response.Message);
        Assert.False(target.HasExited);
    }

    [Theory]
    [InlineData("ArbitraryWorker.exe")]
    [InlineData("ColorVisionHelper.exe")]
    [InlineData("svchost.exe")]
    public void ValidTicketAndExactIdentityCannotTerminateAnUnlistedProgram(string fileName)
    {
        Process target = StartProbe(fileName);
        ServiceHostResponse response = SendAuthorized(CreateRequest(target));
        Assert.False(response.Success);
        Assert.Equal("process_target_not_allowed", response.Message);
        Assert.False(target.HasExited);
    }

    [Fact]
    public void LegacyCommandCannotUseAnUnknownServiceNameToTerminateAnAllowlistedProgram()
    {
        Process target = StartProbe("mysqld.exe");
        ServiceHostResponse response = SendAuthorized(new ServiceHostRequest
        {
            Command = "service-terminate",
            Data = JObject.FromObject(new { serviceName = "UnknownService", executablePath = target.StartInfo.FileName }),
        });
        Assert.False(response.Success);
        Assert.Equal("service_target_not_allowed", response.Message);
        Assert.False(target.HasExited);
    }

    [Fact]
    public void SharedTerminationHelperCannotBypassTheProgramAllowlist()
    {
        Process target = StartProbe("UnlistedWorker.exe");
        var error = Assert.Throws<InvalidOperationException>(() => ProcessCommandService.TerminateProcess(target, false, 500));
        Assert.Equal("process_target_not_allowed", error.Message);
        Assert.False(target.HasExited);
    }

    [Fact]
    public void AllowlistedTargetCanRunFromAnArbitraryDirectory()
    {
        Process target = StartProbe(Path.Combine("Portable folder", "移动后", "ColorVision.exe"));
        ServiceHostResponse response = SendAuthorized(CreateRequest(target));
        Assert.True(response.Success, response.ToDisplayText());
        Assert.True(target.HasExited);
    }

    [Fact]
    public void LegacyServiceTerminationPreservesOtherInstallations()
    {
        Process target = StartProbe("RegWindowsService.exe");
        Process other = StartProbe(Path.Combine("OtherInstallation", "RegWindowsService.exe"));

        // Supply only owned probes; discovery of real SCM services is not part of this test.
        var (terminated, remaining) = ProcessCommandService.TerminateServiceProcesses([target, other], target.StartInfo.FileName, 5000);

        Assert.Equal([target.Id], terminated);
        Assert.Empty(remaining);
        Assert.True(target.HasExited);
        Assert.False(other.HasExited);
    }

    [Fact]
    public async Task LegacyServiceClientRejectsUnknownServicesOverPipe()
    {
        Process target = StartProbe("RegWindowsService.exe");
        string pipeName = "ColorVision-ProcessApi-" + Guid.NewGuid().ToString("N");
        using ServiceHostPipeServer server = CreatePipeServer(pipeName);
        Task run = server.RunAsync(CancellationToken.None);
        try
        {
            var client = new UiServiceHostClient(pipeName);
            var response = await client.TerminateServiceAsync("UnknownService", target.StartInfo.FileName);

            Assert.False(response.Success);
            Assert.Equal("service_target_not_allowed", response.Message);
            Assert.False(target.HasExited);
        }
        finally
        {
            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await run;
        }
    }

    [Fact]
    public async Task GenericClientReachesTerminationOverPipeAndPreservesAnotherProcessAtTheSamePath()
    {
        Process target = StartProbe("ColorVision.exe");
        Process other = StartProbe("ColorVision.exe");
        string pipeName = "ColorVision-ProcessApi-" + Guid.NewGuid().ToString("N");
        using ServiceHostPipeServer server = CreatePipeServer(pipeName);
        Task run = server.RunAsync(CancellationToken.None);
        try
        {
            var client = new UiServiceHostClient(pipeName);
            await client.TerminateProcessAsync(target.Id, target.StartTime.ToUniversalTime(), target.StartInfo.FileName, new Progress<string>());

            Assert.True(target.HasExited);
            Assert.False(other.HasExited);
        }
        finally
        {
            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await run;
        }
    }

    private ServiceHostPipeServer CreatePipeServer(string pipeName) => new(
        _handler.Handle,
        (NamedPipeServerStream _, out ServiceHostRequestContext context, out string error) =>
        {
            // Exercise production transport and ticket handling without impersonating a production caller.
            context = _context;
            error = string.Empty;
            return true;
        },
        pipeName,
        () => new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous));

    private ServiceHostRequest CreateRequest(Process process) => new()
    {
        Command = "process-terminate",
        Data = JObject.FromObject(new
        {
            processId = process.Id,
            startTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks,
            executablePath = process.StartInfo.FileName,
            notAfterUtcTicks = DateTime.UtcNow.AddSeconds(10).Ticks,
        }),
    };

    private ServiceHostResponse SendAuthorized(ServiceHostRequest request)
    {
        ServiceHostResponse ticket = _handler.Handle(new ServiceHostRequest
        {
            Command = "issue-broker-ticket",
            OperationId = request.OperationId,
            Data = JObject.FromObject(new { command = request.Command }),
        }, _context);
        Assert.True(ticket.Success, ticket.ToDisplayText());
        request.BrokerTicket = ticket.Data!["ticket"]!.Value<string>();
        return _handler.Handle(request, _context);
    }

    private Process StartProbe(string fileName)
    {
        string executablePath = Path.Combine(_directory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        if (!File.Exists(executablePath))
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executablePath);
        Process process = Process.Start(new ProcessStartInfo(executablePath, "-t 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true })!;
        _processes.Add(process);
        Assert.True(SpinWait.SpinUntil(() =>
        {
            process.Refresh();
            return process.MainModule != null;
        }, TimeSpan.FromSeconds(5)), "The test probe did not finish loading its executable.");
        return process;
    }

    public void Dispose()
    {
        foreach (Process process in _processes)
        {
            if (!process.HasExited) process.Kill();
            process.WaitForExit(5000);
            process.Dispose();
        }
        Directory.Delete(_directory, true);
    }
}
