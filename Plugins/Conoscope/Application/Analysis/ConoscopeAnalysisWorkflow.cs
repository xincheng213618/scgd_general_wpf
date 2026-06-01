using Conoscope.Analysis;
using System; // Exception

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

        public (ColorGamutComputationResult? Result, string? Error) ComputeGamut(ColorGamutStandard standard)
        {
            if (gamutRedCapture == null || gamutGreenCapture == null || gamutBlueCapture == null)
                return (null, Properties.Resources.MsgNeedRGBData);

            if (standard == null)
                return (null, Properties.Resources.MsgSelectGamutStandard);

            try
            {
                return (batchColorGamutCalculator.Calculate(gamutRedCapture, gamutGreenCapture, gamutBlueCapture, standard), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public (ContrastComputationResult? Result, string? Error) ComputeContrast()
        {
            if (contrastWhiteCapture == null || contrastBlackCapture == null)
                return (null, Properties.Resources.MsgNeedWhiteBlackData);

            try
            {
                return (batchContrastCalculator.Calculate(contrastWhiteCapture, contrastBlackCapture), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
    }
}
