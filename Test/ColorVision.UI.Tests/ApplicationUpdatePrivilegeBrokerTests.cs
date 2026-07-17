using ColorVision.UI.ServiceHost;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class ApplicationUpdatePrivilegeBrokerTests
    {
        [Fact]
        public void WritablePortableDirectoryDoesNotCallServiceHost()
        {
            string writableDirectory = Directory.CreateTempSubdirectory("ColorVisionPortableUpdate-").FullName;
            bool serviceCalled = false;

            try
            {
                bool result = ApplicationUpdatePrivilegeBroker.TryPrepareApplicationDirectory(
                    writableDirectory,
                    () =>
                    {
                        serviceCalled = true;
                        return Task.FromResult(new ServiceHostResponse { Success = false });
                    });

                Assert.True(result);
                Assert.False(serviceCalled);
            }
            finally
            {
                Directory.Delete(writableDirectory, true);
            }
        }

        [Fact]
        public void UnwritableOrMissingDirectoryFallsBackToServiceHost()
        {
            string missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            bool serviceCalled = false;

            bool result = ApplicationUpdatePrivilegeBroker.TryPrepareApplicationDirectory(
                missingDirectory,
                () =>
                {
                    serviceCalled = true;
                    return Task.FromResult(new ServiceHostResponse { Success = true });
                });

            Assert.True(result);
            Assert.True(serviceCalled);
        }

        [Fact]
        public void SuccessfulServiceRequestAvoidsUac()
        {
            bool result = ApplicationUpdatePrivilegeBroker.TryPrepareApplicationDirectory(
                () => Task.FromResult(new ServiceHostResponse { Success = true }));

            Assert.True(result);
        }

        [Fact]
        public void RejectedServiceRequestUsesUac()
        {
            bool result = ApplicationUpdatePrivilegeBroker.TryPrepareApplicationDirectory(
                () => Task.FromResult(new ServiceHostResponse { Success = false, Message = "unsupported" }));

            Assert.False(result);
        }

        [Fact]
        public void MissingServiceUsesUac()
        {
            bool result = ApplicationUpdatePrivilegeBroker.TryPrepareApplicationDirectory(
                () => Task.FromException<ServiceHostResponse>(new IOException("pipe unavailable")));

            Assert.False(result);
        }
    }
}
