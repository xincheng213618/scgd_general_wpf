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
    }
}
