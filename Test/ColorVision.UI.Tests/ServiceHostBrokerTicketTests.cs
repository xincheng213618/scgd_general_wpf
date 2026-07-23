using ColorVisionServiceHost;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using System.IO.Pipes;
using System.Text;
using ServiceHostClient = ColorVision.UI.ServiceHost.ColorVisionServiceHostClient;

namespace ColorVision.UI.Tests
{
    public sealed class ServiceHostBrokerTicketTests
    {
        [Theory]
        [InlineData("self-update")]
        [InlineData("prepare-application-update")]
        public void UpdateOnlyCommandsUseDirectCallerAndPayloadValidation(string command)
        {
            Assert.False(ServiceHostClient.RequiresBrokerTicket(command));
            Assert.False(ServiceHostCommandHandler.RequiresBrokerTicket(command));
        }

        [Theory]
        [InlineData("service-install")]
        [InlineData("service-restart")]
        [InlineData("firewall-allow-application")]
        [InlineData("registry-set-values")]
        [InlineData("registry-delete-key")]
        [InlineData("begin-application-update-scan-protection")]
        [InlineData("complete-application-update-scan-protection")]
        [InlineData("com0com-status")]
        [InlineData("com0com-list")]
        [InlineData("com0com-create-pair")]
        [InlineData("com0com-delete-pair")]
        public void OtherPrivilegedCommandsStillRequireBrokerTicket(string command)
        {
            Assert.True(ServiceHostClient.RequiresBrokerTicket(command));
            Assert.True(ServiceHostCommandHandler.RequiresBrokerTicket(command));
        }

        [Fact]
        public async Task PrepareApplicationUpdateSendsPackageInSingleDirectRequest()
        {
            string pipeName = $"ColorVisionServiceHostTest-{Guid.NewGuid():N}";
            ColorVision.UI.ServiceHost.ServiceHostRequest? capturedRequest = null;
            using NamedPipeServerStream server = new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            Task serverTask = Task.Run(async () =>
            {
                await server.WaitForConnectionAsync();
                using StreamReader reader = new(server, Encoding.UTF8, false, leaveOpen: true);
                using StreamWriter writer = new(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                string requestJson = await reader.ReadLineAsync() ?? throw new InvalidOperationException("Missing request.");
                capturedRequest = JsonConvert.DeserializeObject<ColorVision.UI.ServiceHost.ServiceHostRequest>(requestJson);
                string responseJson = JsonConvert.SerializeObject(new ColorVision.UI.ServiceHost.ServiceHostResponse
                {
                    RequestId = capturedRequest?.RequestId ?? string.Empty,
                    Success = true,
                    Message = "application update prepared",
                }, ColorVision.UI.ServiceHost.ServiceHostProtocol.JsonSettings);
                await writer.WriteLineAsync(responseJson);
            });

            ServiceHostClient client = new(pipeName);
            ColorVision.UI.ServiceHost.ServiceHostResponse response = await client.PrepareApplicationUpdateAsync(
                @"D:\Update Stage\ServiceHost",
                TimeSpan.FromSeconds(5));
            await serverTask;

            Assert.True(response.Success);
            Assert.NotNull(capturedRequest);
            Assert.Equal("prepare-application-update", capturedRequest.Command);
            Assert.Null(capturedRequest.BrokerTicket);
            Assert.Equal(@"D:\Update Stage\ServiceHost", capturedRequest.Data?["serviceHostPackageDirectory"]?.ToString());
        }

        [Fact]
        public void BrokerTicketIsBoundToCommandCallerAndOperationAndIsSingleUse()
        {
            ServiceHostBrokerTicketService tickets = new();
            ServiceHostRequestContext context = CreateContext();
            ServiceHostRequest request = new()
            {
                Command = "issue-broker-ticket",
                OperationId = "operation-1",
                Data = JObject.FromObject(new { command = "service-restart" }),
            };
            string ticket = tickets.Issue(request, context, "service-restart");
            ServiceHostRequest protectedRequest = new()
            {
                Command = "service-restart",
                OperationId = "operation-1",
                BrokerTicket = ticket,
            };

            Assert.True(tickets.ValidateAndConsume(protectedRequest, context, out string firstError), firstError);
            Assert.False(tickets.ValidateAndConsume(protectedRequest, context, out string replayError));
            Assert.Equal("broker_ticket_replayed", replayError);
        }

        [Fact]
        public void BrokerTicketRejectsChangedCommandOrCaller()
        {
            ServiceHostBrokerTicketService tickets = new();
            ServiceHostRequestContext context = CreateContext();
            ServiceHostRequest issue = new() { OperationId = "operation-2" };
            string ticket = tickets.Issue(issue, context, "service-restart");

            ServiceHostRequest changedCommand = new()
            {
                Command = "service-install", OperationId = "operation-2", BrokerTicket = ticket,
            };
            Assert.False(tickets.ValidateAndConsume(changedCommand, context, out string commandError));
            Assert.Equal("broker_ticket_scope_mismatch_or_expired", commandError);

            ServiceHostRequest correctCommand = new()
            {
                Command = "service-restart", OperationId = "operation-2", BrokerTicket = ticket,
            };
            ServiceHostRequestContext changedCaller = new()
            {
                ProcessId = 4321,
                UserSid = context.UserSid,
                UserName = context.UserName,
                ProcessPath = context.ProcessPath,
                ProcessSha256 = context.ProcessSha256,
            };
            Assert.False(tickets.ValidateAndConsume(correctCommand, changedCaller, out string callerError));
            Assert.Equal("broker_ticket_scope_mismatch_or_expired", callerError);
        }

        private static ServiceHostRequestContext CreateContext() => new()
        {
            ProcessId = 1234,
            UserSid = "S-1-5-21-test",
            UserName = "test-user",
            ProcessPath = @"C:\Program Files\ColorVision\ColorVision.exe",
            ProcessSha256 = new string('a', 64),
        };
    }
}
