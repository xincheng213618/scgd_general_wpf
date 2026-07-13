using OpenCvSharp;
using ProjectARVRPro.Process.DemuraAOI;
using Xunit;

namespace ProjectARVRPro.Tests
{
    public class DemuraAoiTests
    {
        [Fact]
        public void UniformityConstantSingleChannelImageReturnsOne()
        {
            using var image = new Mat(400, 400, MatType.CV_16UC1, Scalar.All(1000));

            W255UniformityResult result = W255UniformityCalculator.Calculate(image, 30);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(9, result.PointMeans.Count);
            Assert.Equal(1, result.Uniformity, 12);
        }

        [Fact]
        public void UniformityOneLowerSamplingAreaReturnsMinOverMax()
        {
            using var image = new Mat(400, 400, MatType.CV_16UC1, Scalar.All(1000));
            Cv2.Circle(image, new Point(100, 100), 30, Scalar.All(500), -1);

            W255UniformityResult result = W255UniformityCalculator.Calculate(image, 30);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(0.5, result.Uniformity, 6);
        }

        [Fact]
        public void UniformityThreeChannelImageReturnsDataError()
        {
            using var image = new Mat(400, 400, MatType.CV_8UC3, Scalar.All(100));

            W255UniformityResult result = W255UniformityCalculator.Calculate(image, 30);

            Assert.False(result.Success);
            Assert.Contains("单通道", result.ErrorMessage);
        }

        [Fact]
        public void EvaluateCompleteValidDataReturnsPass()
        {
            DemuraAoiParseResult parsed = CreateValidParseResult();
            var recipe = new DemuraAoiRecipeConfig();

            DemuraAoiEvaluationResult result = DemuraAoiEvaluator.Evaluate(parsed, recipe);

            Assert.Equal(DemuraAoiOutcome.Pass, result.Outcome);
            Assert.All(result.Items, item => Assert.True(item.TestResult, item.Name));
        }

        [Fact]
        public void EvaluateMissingRequiredDataReturnsDataError()
        {
            var parsed = new DemuraAoiParseResult();
            parsed.DataErrors.Add("missing result");

            DemuraAoiEvaluationResult result = DemuraAoiEvaluator.Evaluate(parsed, new DemuraAoiRecipeConfig());

            Assert.Equal(DemuraAoiOutcome.DataError, result.Outcome);
            Assert.False(result.Items.Single(item => item.Name == "AOIDataIntegrity").TestResult);
        }

        [Fact]
        public void EvaluateDisallowedGradeReturnsSpecificationNg()
        {
            DemuraAoiParseResult parsed = CreateValidParseResult();
            parsed.Grading!.GradeLevel = "SOSO";

            DemuraAoiEvaluationResult result = DemuraAoiEvaluator.Evaluate(parsed, new DemuraAoiRecipeConfig());

            Assert.Equal(DemuraAoiOutcome.SpecificationNg, result.Outcome);
            Assert.False(result.Items.Single(item => item.Name == "AOIGrade").TestResult);
        }

        [Fact]
        public void EvaluateEnabledNumericLimitReturnsSpecificationNg()
        {
            DemuraAoiParseResult parsed = CreateValidParseResult();
            parsed.Grading!.MaxDefectDensity = 2;
            var recipe = new DemuraAoiRecipeConfig
            {
                MaxDefectDensity = new DemuraAoiRangeRecipe(0, 1, true)
            };

            DemuraAoiEvaluationResult result = DemuraAoiEvaluator.Evaluate(parsed, recipe);

            Assert.Equal(DemuraAoiOutcome.SpecificationNg, result.Outcome);
            Assert.False(result.Items.Single(item => item.Name == "MaxDefectDensity").TestResult);
        }

        [Fact]
        public void EvaluateSensorAndSpectrometerResultsAddsDisplayOnlyItems()
        {
            DemuraAoiParseResult parsed = CreateValidParseResult();
            parsed.Sensor = new DemuraAoiSensorData
            {
                Channel = 1,
                Voltages = CreateSensorValues(1.1),
                Currents = CreateSensorValues(10)
            };
            parsed.Spectrometer = new DemuraAoiSpectrometerData
            {
                Luminance = 123.4,
                X = 0.31,
                Y = 0.32,
                CorrelatedColorTemperature = 6500,
                DominantWavelengthColor = "#AABBCC"
            };

            DemuraAoiEvaluationResult result = DemuraAoiEvaluator.Evaluate(parsed, new DemuraAoiRecipeConfig());

            Assert.Equal(DemuraAoiOutcome.Pass, result.Outcome);
            Assert.Equal(4, result.Items.Count(item => item.Name.StartsWith("Sensor_Voltage_", StringComparison.Ordinal)));
            Assert.Equal(4, result.Items.Count(item => item.Name.StartsWith("Sensor_Current_", StringComparison.Ordinal)));
            Assert.Contains(result.Items, item => item.Name == "Spectrometer_Lv" && item.TestValue == "123.4");
            Assert.Contains(result.Items, item => item.Name == "Spectrometer_DominantWavelengthColor" && item.TestValue == "#AABBCC");
            Assert.All(result.Items, item => Assert.True(item.TestResult, item.Name));
        }

        [Fact]
        public void ParseSensorResultKeepsOnlyFourRequestedVoltagesAndCurrents()
        {
            const string json = """
                {
                  "Channel": 1,
                  "Voltages": [
                    { "Name": "ELVDD", "Value": 1.099 }, { "Name": "ELVSS", "Value": 2.2 },
                    { "Name": "VDIO", "Value": 1.1 }, { "Name": "VCI", "Value": 1.799 },
                    { "Name": "VBAT2", "Value": 3.0 }, { "Name": "AVDD", "Value": 0.0 }
                  ],
                  "Currents": [
                    { "Name": "ELVDD", "Value": 230.7 }, { "Name": "ELVSS", "Value": 134.3 },
                    { "Name": "VDIO", "Value": 14.8 }, { "Name": "VCI", "Value": 7.9 },
                    { "Name": "VBAT2", "Value": 0.0 }, { "Name": "AVDD", "Value": 0.0 },
                    { "Name": "VEXT1", "Value": 0.0 }, { "Name": "VEXT2", "Value": 0.0 },
                    { "Name": "VEXT3", "Value": 0.0 }
                  ]
                }
                """;

            DemuraAoiSensorData? result = DemuraAoiParser.ParseSensorResult(284, json);

            Assert.NotNull(result);
            Assert.Equal(1, result.Channel);
            string[] expectedNames = { "ELVDD", "ELVSS", "VDIO", "VCI" };
            Assert.Equal(expectedNames, result.Voltages.Select(item => item.Name));
            Assert.Equal(expectedNames, result.Currents.Select(item => item.Name));
        }

        private static DemuraAoiParseResult CreateValidParseResult()
        {
            return new DemuraAoiParseResult
            {
                W255 = new W255UniformityResult { Success = true, Uniformity = 0.8 },
                Grading = new DemuraAoiGradingData
                {
                    GradeLevel = "WELL",
                    MaxDefectDensity = 0,
                    DarkTotalDefects = 0,
                    BrightTotalDefects = 0
                },
                Black = new DemuraAoiBlackData
                {
                    GradeLevel = "OK",
                    BrightCount = 0
                }
            };
        }

        private static List<DemuraAoiNamedValue> CreateSensorValues(double startValue)
        {
            string[] names = { "ELVDD", "ELVSS", "VDIO", "VCI" };
            return names.Select((name, index) => new DemuraAoiNamedValue { Name = name, Value = startValue + index }).ToList();
        }
    }
}
