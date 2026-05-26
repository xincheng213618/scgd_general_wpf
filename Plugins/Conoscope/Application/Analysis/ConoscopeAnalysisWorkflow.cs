using Conoscope.Analysis;
using System;

namespace Conoscope.ApplicationServices.Analysis
{
    public enum CaptureSlot
    {
        GamutRed,
        GamutGreen,
        GamutBlue,
        ContrastWhite,
        ContrastBlack,
    }

    public sealed class ConoscopeAnalysisWorkflow
    {
        private readonly DefaultBatchColorGamutCalculator batchColorGamutCalculator = new();
        private readonly DefaultBatchContrastCalculator batchContrastCalculator = new();

        private MeasurementCapture? gamutRedCapture;
        private MeasurementCapture? gamutGreenCapture;
        private MeasurementCapture? gamutBlueCapture;
        private MeasurementCapture? contrastWhiteCapture;
        private MeasurementCapture? contrastBlackCapture;

        public MeasurementCapture? GamutRedCapture => gamutRedCapture;
        public MeasurementCapture? GamutGreenCapture => gamutGreenCapture;
        public MeasurementCapture? GamutBlueCapture => gamutBlueCapture;
        public MeasurementCapture? ContrastWhiteCapture => contrastWhiteCapture;
        public MeasurementCapture? ContrastBlackCapture => contrastBlackCapture;

        public bool HasAnyGamutCapture => gamutRedCapture != null || gamutGreenCapture != null || gamutBlueCapture != null;
        public bool HasAnyContrastCapture => contrastWhiteCapture != null || contrastBlackCapture != null;

        public bool CanComputeGamut(ColorGamutStandard? standard)
        {
            return gamutRedCapture != null
                && gamutGreenCapture != null
                && gamutBlueCapture != null
                && standard != null;
        }

        public bool CanComputeContrast
        {
            get { return contrastWhiteCapture != null && contrastBlackCapture != null; }
        }

        public void RecordCapture(CaptureSlot slot, MeasurementCapture capture)
        {
            ArgumentNullException.ThrowIfNull(capture);
            switch (slot)
            {
                case CaptureSlot.GamutRed: gamutRedCapture = capture; break;
                case CaptureSlot.GamutGreen: gamutGreenCapture = capture; break;
                case CaptureSlot.GamutBlue: gamutBlueCapture = capture; break;
                case CaptureSlot.ContrastWhite: contrastWhiteCapture = capture; break;
                case CaptureSlot.ContrastBlack: contrastBlackCapture = capture; break;
            }
        }

        public void ClearGamut()
        {
            gamutRedCapture = null;
            gamutGreenCapture = null;
            gamutBlueCapture = null;
        }

        public void ClearContrast()
        {
            contrastWhiteCapture = null;
            contrastBlackCapture = null;
        }

        public AnalysisWorkflowResult<ColorGamutComputationResult> ComputeGamut(ColorGamutStandard standard)
        {
            if (gamutRedCapture == null || gamutGreenCapture == null || gamutBlueCapture == null)
            {
                return AnalysisWorkflowResult<ColorGamutComputationResult>.Fail(Properties.Resources.MsgNeedRGBData);
            }

            if (standard == null)
            {
                return AnalysisWorkflowResult<ColorGamutComputationResult>.Fail(Properties.Resources.MsgSelectGamutStandard);
            }

            try
            {
                ColorGamutComputationResult result = batchColorGamutCalculator.Calculate(
                    gamutRedCapture, gamutGreenCapture, gamutBlueCapture, standard);
                return AnalysisWorkflowResult<ColorGamutComputationResult>.Ok(result);
            }
            catch (Exception ex)
            {
                return AnalysisWorkflowResult<ColorGamutComputationResult>.Fail(ex.Message);
            }
        }

        public AnalysisWorkflowResult<ContrastComputationResult> ComputeContrast()
        {
            if (contrastWhiteCapture == null || contrastBlackCapture == null)
            {
                return AnalysisWorkflowResult<ContrastComputationResult>.Fail(Properties.Resources.MsgNeedWhiteBlackData);
            }

            try
            {
                ContrastComputationResult result = batchContrastCalculator.Calculate(
                    contrastWhiteCapture, contrastBlackCapture);
                return AnalysisWorkflowResult<ContrastComputationResult>.Ok(result);
            }
            catch (Exception ex)
            {
                return AnalysisWorkflowResult<ContrastComputationResult>.Fail(ex.Message);
            }
        }
    }

    public sealed class AnalysisWorkflowResult<T>
    {
        private AnalysisWorkflowResult(bool isSuccess, T? value, string? errorMessage)
        {
            IsSuccess = isSuccess;
            Value = value;
            ErrorMessage = errorMessage;
        }

        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? ErrorMessage { get; }

        public static AnalysisWorkflowResult<T> Ok(T value)
        {
            return new AnalysisWorkflowResult<T>(true, value, null);
        }

        public static AnalysisWorkflowResult<T> Fail(string errorMessage)
        {
            return new AnalysisWorkflowResult<T>(false, default, errorMessage);
        }
    }
}
