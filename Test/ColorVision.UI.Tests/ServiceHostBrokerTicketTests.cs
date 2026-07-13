using ColorVisionServiceHost;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests
{
    public sealed class ServiceHostBrokerTicketTests
    {
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
