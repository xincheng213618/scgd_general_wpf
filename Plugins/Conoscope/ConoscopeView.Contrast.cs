using Conoscope.Core;
using Conoscope.Presentation.Helpers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void InitializeContrastControls()
        {
            SyncContrastImageKindControl();
            UpdateContrastReferenceUi();
        }

        public ContrastReferenceKind GetCurrentContrastImageKind()
        {
            return contrastImageKind;
        }

        private ContrastReferenceKind GetSelectedContrastImageKind()
        {
            return ComboBoxHelper.GetSelectedEnumByTag(cbContrastReferenceKind, contrastImageKind);
        }

        private ContrastReferenceKind GetRequiredContrastReferenceKind()
        {
            return contrastImageKind == ContrastReferenceKind.Black
                ? ContrastReferenceKind.White
                : ContrastReferenceKind.Black;
        }

        private static string GetContrastImageKindText(ContrastReferenceKind kind)
        {
            return kind == ContrastReferenceKind.Black ? Properties.Resources.ContrastImageBlack : Properties.Resources.ContrastImageWhite;
        }

        private static string GetContrastReferenceKindText(ContrastReferenceKind kind)
        {
            return kind == ContrastReferenceKind.Black ? Properties.Resources.ContrastReferenceBlackField : Properties.Resources.ContrastReferenceWhiteField;
        }

        private void SyncContrastImageKindControl()
        {
            if (cbContrastReferenceKind == null)
            {
                return;
            }

            isUpdatingContrastControls = true;
            try
            {
                ComboBoxHelper.SelectItemByTag(cbContrastReferenceKind, contrastImageKind.ToString());
            }
            finally
            {
                isUpdatingContrastControls = false;
            }
        }

        private void ApplyContrastImageKind(ContrastReferenceKind kind, bool refreshDisplay)
        {
            if (contrastImageKind == kind)
            {
                SyncContrastImageKindControl();
                UpdateContrastReferenceUi();
                RaiseWindowQuickControlStateChanged();
                return;
            }

            contrastImageKind = kind;
            SyncContrastImageKindControl();
            UpdateContrastReferenceUi();
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

        private void UpdateContrastReferenceUi()
        {
            if (tbContrastReferenceStatus == null)
            {
                return;
            }

            tbContrastReferenceStatus.Text = GetContrastReferenceStatusText();
        }

        private string GetContrastReferenceStatusText()
        {
            ContrastReferenceKind imageKind = contrastImageKind;
            ContrastReferenceKind requiredReferenceKind = GetRequiredContrastReferenceKind();
            OpenCvSharp.Mat? requiredReferenceYMat = GlobalReferences.GetContrastReferenceYMat(requiredReferenceKind);

            string requiredStatus = requiredReferenceYMat == null
                ? Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgContrastReferenceMissingStatus, GetContrastImageKindText(imageKind), GetContrastReferenceKindText(requiredReferenceKind))
                : YMat != null && (YMat.Width != requiredReferenceYMat.Width || YMat.Height != requiredReferenceYMat.Height)
                    ? Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgContrastReferenceSizeMismatchStatus, GetContrastImageKindText(imageKind), GetContrastReferenceKindText(requiredReferenceKind))
                    : Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgContrastReferenceUsingStatus, GetContrastImageKindText(imageKind), GetContrastReferenceKindText(requiredReferenceKind));

            string blackStatus = GetSavedContrastReferenceSummary(ContrastReferenceKind.Black);
            string whiteStatus = GetSavedContrastReferenceSummary(ContrastReferenceKind.White);
            return Conoscope.Core.CompositeFormatCache.Format(
                Properties.Resources.MsgContrastReferenceSummary,
                requiredStatus,
                Properties.Resources.ContrastReferenceBlackField,
                blackStatus,
                Properties.Resources.ContrastReferenceWhiteField,
                whiteStatus);
        }

        private string GetSavedContrastReferenceSummary(ContrastReferenceKind referenceKind)
        {
            if (!GlobalReferences.HasContrastReference(referenceKind))
            {
                return Properties.Resources.StateNotSaved;
            }

            return Path.GetFileName(GlobalReferences.GetContrastReferenceFileName(referenceKind)) ?? Properties.Resources.StateSaved;
        }

        private bool EnsureContrastReferenceReady()
        {
            ContrastReferenceKind requiredReferenceKind = GetRequiredContrastReferenceKind();
            OpenCvSharp.Mat? referenceYMat = GlobalReferences.GetContrastReferenceYMat(requiredReferenceKind);
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
                UpdateContrastReferenceUi();
                return false;
            }

            if (YMat != null && (YMat.Width != referenceYMat.Width || YMat.Height != referenceYMat.Height))
            {
                UpdateContrastReferenceUi();
                return false;
            }

            return true;
        }

        private OpenCvSharp.Mat? CreateContrastMat()
        {
            if (YMat == null || !EnsureContrastReferenceReady())
            {
                return null;
            }

            ContrastReferenceKind referenceKind = GetRequiredContrastReferenceKind();
            return ConoscopeColorimetry.CreateContrastMat(YMat, GlobalReferences.GetContrastReferenceYMat(referenceKind)!, referenceKind);
        }

        private double GetContrastValue(int ix, int iy, double currentY)
        {
            ContrastReferenceKind referenceKind = GetRequiredContrastReferenceKind();
            OpenCvSharp.Mat? referenceYMat = GlobalReferences.GetContrastReferenceYMat(referenceKind);
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

        private void ContrastReferenceKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingContrastControls)
            {
                return;
            }

            ApplyContrastImageKind(GetSelectedContrastImageKind(), refreshDisplay: true);
        }

        public void SaveCurrentAsGlobalContrastReference(ContrastReferenceKind referenceKind)
        {
            if (YMat == null)
            {
                MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleContrastCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GlobalReferences.SaveContrastReference(referenceKind, YMat, Filename);
            ConoscopeModuleService.RefreshAllReferenceState();
        }

        private void btnCalculateContrast_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureContrastReferenceReady())
            {
                return;
            }

            bool refreshAfterSelection = GetSelectedDisplayChannel() == ExportChannel.Contrast;
            ComboBoxHelper.SelectItemByTag(cbDisplayChannel, ExportChannel.Contrast.ToString());
            if (refreshAfterSelection && GetSelectedDisplayChannel() == ExportChannel.Contrast && HasXyzData())
            {
                RefreshDisplayedImage();
                UpdateReferencePlot();
            }
        }
    }
}
