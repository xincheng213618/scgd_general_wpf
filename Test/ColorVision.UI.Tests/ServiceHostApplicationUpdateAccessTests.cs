using ColorVisionServiceHost;
using System.IO;
using System.Security.Principal;

namespace ColorVision.UI.Tests
{
    public sealed class ServiceHostApplicationUpdateAccessTests
    {
        [Fact]
        public void UpdateAccessIsRestrictedToCallingColorVisionDirectoryAndUser()
        {
            string tempDirectory = Directory.CreateTempSubdirectory("ColorVisionServiceHostTest-").FullName;
            string processPath = Path.Combine(tempDirectory, "ColorVision.exe");
            File.WriteAllBytes(processPath, []);

            try
            {
                string userSid = WindowsIdentity.GetCurrent().User!.Value;
                string[] arguments = ServiceHostCommandHandler.BuildApplicationUpdateAccessArguments(new ServiceHostRequestContext
                {
                    ProcessPath = processPath,
                    UserSid = userSid,
                });

                Assert.Equal(Path.GetFullPath(tempDirectory), arguments[0]);
                Assert.Equal("/grant:r", arguments[1]);
                Assert.Equal($"*{userSid}:(OI)(CI)M", arguments[2]);
                Assert.Equal(new[] { "/T", "/C", "/Q" }, arguments[3..]);
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Fact]
        public void UpdateAccessRejectsAnyOtherExecutable()
        {
            string tempDirectory = Directory.CreateTempSubdirectory("ColorVisionServiceHostTest-").FullName;
            string processPath = Path.Combine(tempDirectory, "Other.exe");
            File.WriteAllBytes(processPath, []);

            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    ServiceHostCommandHandler.BuildApplicationUpdateAccessArguments(new ServiceHostRequestContext
                    {
                        ProcessPath = processPath,
                        UserSid = WindowsIdentity.GetCurrent().User!.Value,
                    }));
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Fact]
        public void PrepareApplicationUpdateExecutesForSpecialCharacterDirectory()
        {
            string tempDirectory = Path.Combine(
                Path.GetTempPath(),
                $"Color Vision Update 100% & Brackets [{Guid.NewGuid():N}]");
            Directory.CreateDirectory(tempDirectory);
            string processPath = Path.Combine(tempDirectory, "ColorVision.exe");
            File.WriteAllBytes(processPath, []);

            try
            {
                string userSid = WindowsIdentity.GetCurrent().User!.Value;
                ServiceHostResponse response = new ServiceHostCommandHandler().Handle(
                    new ServiceHostRequest
                    {
                        Command = "prepare-application-update",
                    },
                    new ServiceHostRequestContext
                    {
                        ProcessId = Environment.ProcessId,
                        ProcessPath = processPath,
                        UserSid = userSid,
                    });

                Assert.True(response.Success, response.ToDisplayText());
                Assert.Equal(Path.GetFullPath(tempDirectory), response.Data?["applicationDirectory"]?.ToObject<string>());
                Assert.Equal(userSid, response.Data?["userSid"]?.ToObject<string>());
                Assert.Equal(0, response.Data?["result"]?["exitCode"]?.ToObject<int>());
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }
}
