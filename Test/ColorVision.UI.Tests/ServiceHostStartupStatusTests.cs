using ColorVisionServiceHost;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests
{
    public sealed class ServiceHostStartupStatusTests
    {
        [Fact]
        public void StartupStatusAcceptsNullDetails()
        {
            ServiceHostResponse response = new ServiceHostCommandHandler().Handle(
                new ServiceHostRequest
                {
                    Command = "application-startup-status",
                    Data = new JObject
                    {
                        ["state"] = "begin",
                        ["stage"] = "AppConstructed",
                        ["details"] = JValue.CreateNull(),
                    },
                },
                new ServiceHostRequestContext
                {
                    ProcessId = Environment.ProcessId,
                    ProcessPath = @"C:\Program Files\ColorVision Inc\ColorVision\ColorVision.exe",
                });

            Assert.True(response.Success, response.ToDisplayText());
            Assert.Equal("startup_status_accepted", response.Message);
        }
    }
}
