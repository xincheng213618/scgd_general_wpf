using ColorVision.UI.ServiceHost;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class ApplicationUpdatePrivilegeBrokerTests
    {
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
