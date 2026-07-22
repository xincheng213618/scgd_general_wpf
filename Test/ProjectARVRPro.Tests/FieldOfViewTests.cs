using Newtonsoft.Json;
using ProjectARVRPro.Exports;
using ProjectARVRPro.Process.RGB.FieldOfView;
using System.IO;
using Xunit;

namespace ProjectARVRPro.Tests
{
    public class FieldOfViewTests
    {
        [Fact]
        public void OutputKeyDefaultsToWhiteAndNormalizesInput()
        {
            var config = new FieldOfViewProcessConfig();

            Assert.Equal("White", config.GetOutputKey());

            config.Key = "  LeftEye  ";
            Assert.Equal("LeftEye", config.GetOutputKey());

            config.Key = " ";
            Assert.Equal("White", config.GetOutputKey());
        }

        [Fact]
        public void WhiteKeyWritesDictionaryAndW51CompatibilityResult()
        {
            var result = new ObjectiveTestResult();
            var fieldOfViewResult = CreateResult();

            result.SetFieldOfViewResult("White", fieldOfViewResult);

            Assert.Same(fieldOfViewResult, result.FieldOfViewTestResults["White"]);
            Assert.Same(fieldOfViewResult.HorizontalFieldOfViewAngle, result.W51TestResult.HorizontalFieldOfViewAngle);
            Assert.Equal(24.1, result.W51TestResult.HorizontalFieldOfViewAngle.Value);
            Assert.Contains("\"FieldOfViewTestResults\":{\"White\"", JsonConvert.SerializeObject(result));
            Assert.Contains("\"W51TestResult\"", JsonConvert.SerializeObject(result));
        }

        [Fact]
        public void EquivalentKeyCasingUpdatesOneDictionaryEntry()
        {
            var result = new ObjectiveTestResult();
            result.SetFieldOfViewResult("White", CreateResult());

            result.SetFieldOfViewResult(" white ", CreateResult());

            Assert.Single(result.FieldOfViewTestResults);
            Assert.True(result.FieldOfViewTestResults.ContainsKey("White"));
            Assert.NotNull(result.W51TestResult);
        }

        [Fact]
        public void NonWhiteKeyDoesNotPopulateW51CompatibilityResult()
        {
            var result = new ObjectiveTestResult();

            result.SetFieldOfViewResult("LeftEye", CreateResult());
            var record = ObjectiveTestResultRecord.Create(new ProjectARVRReuslt(), result);

            Assert.Null(result.W51TestResult);
            Assert.True(result.FieldOfViewTestResults.ContainsKey("LeftEye"));
            Assert.False(record.HasW51);
            Assert.True(record.HasFov);
        }

        [Fact]
        public void ResolverReadsTypedDictionariesCaseInsensitively()
        {
            var result = new ObjectiveTestResult();
            result.SetFieldOfViewResult("White", CreateResult());
            var resolver = new ObjectiveTestResultValueResolver(result);

            Assert.Equal(24.1, resolver.Find("white", "HorizontalFieldOfViewAngle")?.Value);
            Assert.Equal(22.2, resolver.Find("WHITE", "Vertical_Field of_View_Angle")?.Value);
        }

        [Fact]
        public void CsvUsesCanonicalDictionaryRowsWithoutCompatibilityDuplicates()
        {
            string filePath = Path.GetTempFileName();
            try
            {
                var result = new ObjectiveTestResult();
                result.SetFieldOfViewResult("White", CreateResult());

                ObjectiveTestResultCsvExporter.ExportToCsv(result, filePath);
                string csv = File.ReadAllText(filePath);

                Assert.Contains("White,Horizontal_Field_Of_View_Angle", csv);
                Assert.DoesNotContain("W51,Horizontal_Field_Of_View_Angle", csv);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        private static FieldOfViewTestResult CreateResult()
        {
            return new FieldOfViewTestResult
            {
                HorizontalFieldOfViewAngle = new ObjectiveTestItem
                {
                    Name = "Horizontal_Field_Of_View_Angle",
                    Value = 24.1,
                    TestValue = "24.1000",
                    Unit = "degree"
                },
                VerticalFieldOfViewAngle = new ObjectiveTestItem
                {
                    Name = "Vertical_Field of_View_Angle",
                    Value = 22.2,
                    TestValue = "22.2000",
                    Unit = "degree"
                },
                DiagonalFieldOfViewAngle = new ObjectiveTestItem
                {
                    Name = "Diagonal_Field_of_View_Angle",
                    Value = 12.3,
                    TestValue = "12.3000",
                    Unit = "degree"
                }
            };
        }
    }
}
