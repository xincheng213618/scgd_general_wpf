using ProjectARVRPro.Process.MTF;
using ProjectARVRPro.Process.MTF.MTFH;
using ProjectARVRPro.Process.MTF.MTFHV;
using ProjectARVRPro.Process.MTF.MTFHV048;
using ProjectARVRPro.Process.MTF.MTFHV058;
using ProjectARVRPro.Process.MTF.MTFHVDynamic;
using ProjectARVRPro.Process.MTF.MTFV;
using Xunit;

namespace ProjectARVRPro.Tests
{
    public class MTFProcessConfigDefaultsTests
    {
        [Fact]
        public void AllMtfProcessConfigsDefaultToThreeDecimalPlaces()
        {
            string[] showConfigs =
            [
                new MTFProcessConfig().ShowConfig,
                new MTFHProcessConfig().ShowConfig,
                new MTFHVProcessConfig().ShowConfig,
                new MTFHV048ProcessConfig().ShowConfig,
                new MTFHV058ProcessConfig().ShowConfig,
                new MTFHVDynamicProcessConfig().ShowConfig,
                new MTFVProcessConfig().ShowConfig
            ];

            Assert.All(showConfigs, showConfig => Assert.Equal("F3", showConfig));
        }
    }
}
