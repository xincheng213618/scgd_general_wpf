using Conoscope.Analysis;
using Conoscope.ApplicationServices.Analysis;
using Conoscope.Core;

namespace Conoscope.Tests
{
    public class ConoscopeAnalysisWorkflowTests
    {
        private static MeasurementCapture CreateCapture(string slotName, int pointCount = 1)
        {
            List<MeasurementPoint> points = new();
            for (int i = 0; i < pointCount; i++)
            {
                var chromaticity = new ConoscopeChromaticity(0.3, 0.3, 0.2, 0.4, 6500);
                var measurement = new ImageMeasurement($"test_{slotName}_{i}.cvcie", 0.5, 0.6, 0.4, chromaticity);
                points.Add(new MeasurementPoint(
                    $"Point_{i}",
                    $"Point {i}",
                    measurement,
                    i * 10.0,
                    i * 5.0,
                    1.0));
            }

            return MeasurementCapture.FromFocusPoints(slotName, $"Source_{slotName}", points);
        }

        [Fact]
        public void NewWorkflow_HasNoCaptures()
        {
            var workflow = new ConoscopeAnalysisWorkflow();

            Assert.Null(workflow.GamutRedCapture);
            Assert.Null(workflow.GamutGreenCapture);
            Assert.Null(workflow.GamutBlueCapture);
            Assert.Null(workflow.ContrastWhiteCapture);
            Assert.Null(workflow.ContrastBlackCapture);
            Assert.False(workflow.HasAnyGamutCapture);
            Assert.False(workflow.HasAnyContrastCapture);
        }

        [Fact]
        public void CanComputeGamut_ReturnsFalse_WhenMissingCaptures()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            var standard = ColorGamutStandards.All[0];

            Assert.False(workflow.CanComputeGamut(standard));

            workflow.RecordCapture(CaptureSlot.GamutRed, CreateCapture("R"));
            Assert.False(workflow.CanComputeGamut(standard));

            workflow.RecordCapture(CaptureSlot.GamutGreen, CreateCapture("G"));
            Assert.False(workflow.CanComputeGamut(standard));
        }

        [Fact]
        public void CanComputeGamut_ReturnsFalse_WhenStandardIsNull()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.GamutRed, CreateCapture("R"));
            workflow.RecordCapture(CaptureSlot.GamutGreen, CreateCapture("G"));
            workflow.RecordCapture(CaptureSlot.GamutBlue, CreateCapture("B"));

            Assert.False(workflow.CanComputeGamut(null));
        }

        [Fact]
        public void CanComputeGamut_ReturnsTrue_WhenAllCapturesAndStandardPresent()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.GamutRed, CreateCapture("R"));
            workflow.RecordCapture(CaptureSlot.GamutGreen, CreateCapture("G"));
            workflow.RecordCapture(CaptureSlot.GamutBlue, CreateCapture("B"));

            Assert.True(workflow.CanComputeGamut(ColorGamutStandards.All[0]));
        }

        [Fact]
        public void ComputeGamut_ReturnsFailure_WhenMissingRedCapture()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.GamutGreen, CreateCapture("G"));
            workflow.RecordCapture(CaptureSlot.GamutBlue, CreateCapture("B"));

            var result = workflow.ComputeGamut(ColorGamutStandards.All[0]);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.Value);
        }

        [Fact]
        public void ComputeGamut_ReturnsFailure_WhenMissingGreenCapture()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.GamutRed, CreateCapture("R"));
            workflow.RecordCapture(CaptureSlot.GamutBlue, CreateCapture("B"));

            var result = workflow.ComputeGamut(ColorGamutStandards.All[0]);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void ComputeGamut_ReturnsFailure_WhenMissingBlueCapture()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.GamutRed, CreateCapture("R"));
            workflow.RecordCapture(CaptureSlot.GamutGreen, CreateCapture("G"));

            var result = workflow.ComputeGamut(ColorGamutStandards.All[0]);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void ComputeGamut_ReturnsSuccess_WhenAllCapturesPresent()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.GamutRed, CreateCapture("R"));
            workflow.RecordCapture(CaptureSlot.GamutGreen, CreateCapture("G"));
            workflow.RecordCapture(CaptureSlot.GamutBlue, CreateCapture("B"));

            var result = workflow.ComputeGamut(ColorGamutStandards.All[0]);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Null(result.ErrorMessage);
            Assert.Equal(ColorGamutStandards.All[0].Name, result.Value.Standard.Name);
        }

        [Fact]
        public void CanComputeContrast_ReturnsFalse_WhenMissingCaptures()
        {
            var workflow = new ConoscopeAnalysisWorkflow();

            Assert.False(workflow.CanComputeContrast);

            workflow.RecordCapture(CaptureSlot.ContrastWhite, CreateCapture("White"));
            Assert.False(workflow.CanComputeContrast);
        }

        [Fact]
        public void CanComputeContrast_ReturnsTrue_WhenBothCapturesPresent()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.ContrastWhite, CreateCapture("White"));
            workflow.RecordCapture(CaptureSlot.ContrastBlack, CreateCapture("Black"));

            Assert.True(workflow.CanComputeContrast);
        }

        [Fact]
        public void ComputeContrast_ReturnsFailure_WhenMissingWhiteCapture()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.ContrastBlack, CreateCapture("Black"));

            var result = workflow.ComputeContrast();

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.Value);
        }

        [Fact]
        public void ComputeContrast_ReturnsFailure_WhenMissingBlackCapture()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.ContrastWhite, CreateCapture("White"));

            var result = workflow.ComputeContrast();

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void ComputeContrast_ReturnsSuccess_WhenBothCapturesPresent()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.ContrastWhite, CreateCapture("White"));
            workflow.RecordCapture(CaptureSlot.ContrastBlack, CreateCapture("Black"));

            var result = workflow.ComputeContrast();

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Null(result.ErrorMessage);
            Assert.Single(result.Value.Points);
        }

        [Fact]
        public void ClearGamut_ResetsAllGamutCaptures()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.GamutRed, CreateCapture("R"));
            workflow.RecordCapture(CaptureSlot.GamutGreen, CreateCapture("G"));
            workflow.RecordCapture(CaptureSlot.GamutBlue, CreateCapture("B"));

            workflow.ClearGamut();

            Assert.Null(workflow.GamutRedCapture);
            Assert.Null(workflow.GamutGreenCapture);
            Assert.Null(workflow.GamutBlueCapture);
            Assert.False(workflow.HasAnyGamutCapture);
        }

        [Fact]
        public void ClearContrast_ResetsAllContrastCaptures()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.ContrastWhite, CreateCapture("White"));
            workflow.RecordCapture(CaptureSlot.ContrastBlack, CreateCapture("Black"));

            workflow.ClearContrast();

            Assert.Null(workflow.ContrastWhiteCapture);
            Assert.Null(workflow.ContrastBlackCapture);
            Assert.False(workflow.HasAnyContrastCapture);
        }

        [Fact]
        public void HasAnyGamutCapture_ReturnsTrue_AfterRecordingOne()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.GamutRed, CreateCapture("R"));

            Assert.True(workflow.HasAnyGamutCapture);
        }

        [Fact]
        public void HasAnyContrastCapture_ReturnsTrue_AfterRecordingOne()
        {
            var workflow = new ConoscopeAnalysisWorkflow();
            workflow.RecordCapture(CaptureSlot.ContrastWhite, CreateCapture("White"));

            Assert.True(workflow.HasAnyContrastCapture);
        }

        [Fact]
        public void AnalysisWorkflowResult_Ok_PreservesValue()
        {
            var result = AnalysisWorkflowResult<int>.Ok(42);

            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void AnalysisWorkflowResult_Fail_PreservesError()
        {
            var result = AnalysisWorkflowResult<int>.Fail("something went wrong");

            Assert.False(result.IsSuccess);
            Assert.Equal(default, result.Value);
            Assert.Equal("something went wrong", result.ErrorMessage);
        }
    }
}
