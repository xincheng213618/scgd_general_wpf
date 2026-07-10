using Newtonsoft.Json;
using ProjectARVRPro.Exports;
using ProjectARVRPro.LegacyARVR;
using ProjectARVRPro.Process.RGB.LuminanceChromaticity;
using Xunit;

namespace ProjectARVRPro.Tests
{
    public class LuminanceChromaticityTests
    {
        [Fact]
        public void OutputKeyDefaultsToWhiteAndNormalizesInput()
        {
            var config = new LuminanceChromaticityProcessConfig();

            Assert.Equal("White", config.GetOutputKey());

            config.Key = "  Red  ";
            Assert.Equal("Red", config.GetOutputKey());

            config.Key = " ";
            Assert.Equal("White", config.GetOutputKey());
        }

        [Fact]
        public void ResultDictionarySupportsResolverAndJsonOutput()
        {
            var result = new ObjectiveTestResult();
            result.LuminanceChromaticityTestResults["White"] = new LuminanceChromaticityTestResult
            {
                CenterLuminance = new ObjectiveTestItem { Name = "CenterLuminance", Value = 123.45 }
            };

            var resolver = new ObjectiveTestResultValueResolver(result);

            Assert.Equal(123.45, resolver.Find("White", "CenterLuminance")?.Value);
            Assert.Contains("\"LuminanceChromaticityTestResults\":{\"White\"", JsonConvert.SerializeObject(result));
        }

        [Fact]
        public void W25UsesLuminanceChromaticityConfigurationAndLegacyOutput()
        {
            var config = new LuminanceChromaticityProcessConfig
            {
                Key = "W25",
                CenterKey = "P_9"
            };
            var result = new ObjectiveTestResult();
            result.LuminanceChromaticityTestResults[config.GetOutputKey()] = new LuminanceChromaticityTestResult
            {
                CenterLuminance = new ObjectiveTestItem { Name = "CenterLuminance", Value = 25.5 }
            };

            var legacy = LegacyARVRConverter.ToLegacy(result);

            Assert.Equal("P_9", config.CenterKey);
            Assert.Equal(25.5, legacy.White1CenterLuminace?.Value);
            Assert.True(legacy.FlowWhite1TestReslut);
        }
    }
}
