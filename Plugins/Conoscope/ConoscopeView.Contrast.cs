using Conoscope.Core;
using System;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        public ContrastReferenceKind GetCurrentContrastImageKind()
        {
            return State.ContrastImageKind;
        }

        private ContrastReferenceKind GetRequiredContrastReferenceKind()
        {
            return State.ContrastImageKind == ContrastReferenceKind.Black
                ? ContrastReferenceKind.White
                : ContrastReferenceKind.Black;
        }

        private static string GetContrastReferenceKindText(ContrastReferenceKind kind)
        {
            return kind == ContrastReferenceKind.Black ? Properties.Resources.ContrastReferenceBlackField : Properties.Resources.ContrastReferenceWhiteField;
        }

        private void ApplyContrastImageKind(ContrastReferenceKind kind, bool refreshDisplay)
        {
            if (State.ContrastImageKind == kind)
            {
                RaiseWindowQuickControlStateChanged();
                return;
            }

            State.ContrastImageKind = kind;
            RaiseWindowQuickControlStateChanged();

            if (!refreshDisplay || GetSelectedDisplayChannel() != ExportChannel.Contrast || !HasXyzData())
            {
                return;
            }

            if (!CanRefreshContrastDisplay())
            {
                return;
            }

            try
            {
                RefreshDisplayedImage();
                UpdateReferencePlot();
            }
            catch (Exception ex)
            {
                log.Error($"切换对比度图像类型失败: {ex.Message}", ex);
                MessageBox.Show(ex.Message, Properties.Resources.GroupContrast, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool EnsureContrastReferenceReady()
        {
            return EnsureContrastReferenceReady(GlobalReferences);
        }

        private bool EnsureContrastReferenceReady(ConoscopeGlobalReferenceStore references)
        {
            ContrastReferenceKind requiredReferenceKind = GetRequiredContrastReferenceKind();
            OpenCvSharp.Mat? referenceYMat = references.GetContrastReferenceYMat(requiredReferenceKind);
            if (referenceYMat == null)
            {
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgSaveContrastReferenceRequired, GetContrastReferenceKindText(requiredReferenceKind)), Properties.Resources.TitleContrastCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (YMat != null && (YMat.Width != referenceYMat.Width || YMat.Height != referenceYMat.Height))
            {
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgContrastReferenceImageSizeMismatch, GetContrastReferenceKindText(requiredReferenceKind)), Properties.Resources.TitleContrastCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool CanRefreshContrastDisplay()
        {
            ContrastReferenceKind requiredReferenceKind = GetRequiredContrastReferenceKind();
            OpenCvSharp.Mat? referenceYMat = GlobalReferences.GetContrastReferenceYMat(requiredReferenceKind);
            if (referenceYMat == null)
            {
                return false;
            }

            if (YMat != null && (YMat.Width != referenceYMat.Width || YMat.Height != referenceYMat.Height))
            {
                return false;
            }

            return true;
        }

        private OpenCvSharp.Mat? CreateContrastMat()
        {
            if (YMat == null)
            {
                return null;
            }

            using ConoscopeRuntimeSnapshot runtime = ConoscopeManager.GetInstance().CaptureRuntimeSnapshot();
            ConoscopeGlobalReferenceStore references = runtime.GlobalReferences;
            if (!EnsureContrastReferenceReady(references))
                return null;

            ContrastReferenceKind referenceKind = GetRequiredContrastReferenceKind();
            return ConoscopeColorimetry.CreateContrastMat(YMat, references.GetContrastReferenceYMat(referenceKind)!, referenceKind);
        }

        private double GetContrastValue(int ix, int iy, double currentY)
        {
            return GetContrastValue(ix, iy, currentY, GlobalReferences);
        }

        private double GetContrastValue(int ix, int iy, double currentY, ConoscopeGlobalReferenceStore references)
        {
            ContrastReferenceKind referenceKind = GetRequiredContrastReferenceKind();
            OpenCvSharp.Mat? referenceYMat = references.GetContrastReferenceYMat(referenceKind);
            if (referenceYMat == null || YMat == null)
            {
                return double.NaN;
            }

            if (YMat.Width != referenceYMat.Width || YMat.Height != referenceYMat.Height)
            {
                return double.NaN;
            }

            if (ix < 0 || iy < 0 || ix >= referenceYMat.Width || iy >= referenceYMat.Height)
            {
                return double.NaN;
            }

            double referenceY = referenceYMat.At<float>(iy, ix);
            return ConoscopeColorimetry.CalculateContrast(currentY, referenceY, referenceKind);
        }

        public void SaveCurrentAsGlobalContrastReference(ContrastReferenceKind referenceKind)
        {
            if (YMat == null)
            {
                MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleContrastCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using ConoscopeRuntimeSnapshot runtime = ConoscopeManager.GetInstance().CaptureRuntimeSnapshot();
            runtime.GlobalReferences.SaveContrastReference(referenceKind, YMat, Filename);
            ConoscopeModuleService.RefreshAllReferenceState();
        }

    }
}
