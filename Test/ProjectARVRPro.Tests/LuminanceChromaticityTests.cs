using Newtonsoft.Json;
using ProjectARVRPro.Exports;
using ProjectARVRPro.LegacyARVR;
using ProjectARVRPro.Process.KeyedResults;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using System.IO;
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
            var luminanceResult = new LuminanceChromaticityTestResult
            {
                CenterCorrelatedColorTemperature = new ObjectiveTestItem { Name = "CenterCorrelatedColorTemperature", Value = 6500 },
                CenterLuminance = new ObjectiveTestItem { Name = "CenterLuminance", Value = 123.45 }
            };
            KeyedTestResultWriter.Write(result, "White", luminanceResult);

            var resolver = new ObjectiveTestResultValueResolver(result);

            Assert.Equal(123.45, resolver.Find("White", "CenterLuminance")?.Value);
            Assert.Equal(123.45, result.W255TestResult.CenterLunimance.Value);
            Assert.Equal(6500, result.W255TestResult.CenterCorrelatedColorTemperature.Value);
            Assert.Contains("\"LuminanceChromaticityTestResults\":{\"White\"", JsonConvert.SerializeObject(result));
            Assert.Contains("\"W255TestResult\"", JsonConvert.SerializeObject(result));
        }

        [Fact]
        public void NonWhiteKeyDoesNotPopulateW255CompatibilityResult()
        {
            var result = new ObjectiveTestResult();

            KeyedTestResultWriter.Write(result, "Red", new LuminanceChromaticityTestResult());

            Assert.Null(result.W255TestResult);
            Assert.True(result.LuminanceChromaticityTestResults.ContainsKey("Red"));
        }

        [Fact]
        public void CsvUsesCanonicalWhiteRowsWithoutW255CompatibilityDuplicates()
        {
            string filePath = Path.GetTempFileName();
            try
            {
                var result = new ObjectiveTestResult();
                KeyedTestResultWriter.Write(result, "White", new LuminanceChromaticityTestResult
                {
                    CenterLuminance = new ObjectiveTestItem
                    {
                        Name = "CenterLuminance",
                        Value = 123.45,
                        TestValue = "123.4500",
                        Unit = "nit"
                    }
                });

                ObjectiveTestResultCsvExporter.ExportToCsv(result, filePath);
                string csv = File.ReadAllText(filePath);

                Assert.Contains("White,CenterLuminance", csv);
                Assert.DoesNotContain("W255,CenterLuminance", csv);
            }
            finally
            {
                File.Delete(filePath);
            }
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
            KeyedTestResultWriter.Write(result, config.GetOutputKey(), new LuminanceChromaticityTestResult
            {
                CenterLuminance = new ObjectiveTestItem { Name = "CenterLuminance", Value = 25.5 }
            });

            var legacy = LegacyARVRConverter.ToLegacy(result);

            Assert.Equal("P_9", config.CenterKey);
            Assert.Equal(25.5, legacy.White1CenterLuminace?.Value);
            Assert.True(legacy.FlowWhite1TestReslut);
        }
    }
}
