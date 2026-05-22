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
            return kind == ContrastReferenceKind.Black ? "暗图" : "亮图";
        }

        private static string GetContrastReferenceKindText(ContrastReferenceKind kind)
        {
            return kind == ContrastReferenceKind.Black ? "黑场" : "白场";
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
                MessageBox.Show(ex.Message, "对比度", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                ? $"当前{GetContrastImageKindText(imageKind)}缺少{GetContrastReferenceKindText(requiredReferenceKind)}基准。"
                : YMat != null && (YMat.Width != requiredReferenceYMat.Width || YMat.Height != requiredReferenceYMat.Height)
                    ? $"当前{GetContrastImageKindText(imageKind)}对应的{GetContrastReferenceKindText(requiredReferenceKind)}基准尺寸与当前图像不一致。"
                    : $"当前{GetContrastImageKindText(imageKind)}将使用{GetContrastReferenceKindText(requiredReferenceKind)}基准。";

            string blackStatus = GetSavedContrastReferenceSummary(ContrastReferenceKind.Black);
            string whiteStatus = GetSavedContrastReferenceSummary(ContrastReferenceKind.White);
            return $"{requiredStatus}{Environment.NewLine}黑场基准: {blackStatus}{Environment.NewLine}白场基准: {whiteStatus}";
        }

        private string GetSavedContrastReferenceSummary(ContrastReferenceKind referenceKind)
        {
            if (!GlobalReferences.HasContrastReference(referenceKind))
            {
                return "未保存";
            }

            return Path.GetFileName(GlobalReferences.GetContrastReferenceFileName(referenceKind)) ?? "已保存";
        }

        private void EnsureContrastReferenceReady()
        {
            ContrastReferenceKind requiredReferenceKind = GetRequiredContrastReferenceKind();
            OpenCvSharp.Mat? referenceYMat = GlobalReferences.GetContrastReferenceYMat(requiredReferenceKind);
            if (referenceYMat == null)
            {
                throw new InvalidOperationException($"请先保存{GetContrastReferenceKindText(requiredReferenceKind)}基准图");
            }

            if (YMat != null && (YMat.Width != referenceYMat.Width || YMat.Height != referenceYMat.Height))
            {
                throw new InvalidOperationException($"{GetContrastReferenceKindText(requiredReferenceKind)}基准图尺寸与当前图像不一致，请重新保存基准图");
            }
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

        private OpenCvSharp.Mat CreateContrastMat()
        {
            if (YMat == null)
            {
                throw new InvalidOperationException(Properties.Resources.XYZDataNotLoaded);
            }

            EnsureContrastReferenceReady();
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
                throw new InvalidOperationException(Properties.Resources.MsgLoadImageFirst);
            }

            GlobalReferences.SaveContrastReference(referenceKind, YMat, Filename);
            ConoscopeModuleService.RefreshAllReferenceState();
        }

        private void btnCalculateContrast_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EnsureContrastReferenceReady();
                bool refreshAfterSelection = GetSelectedDisplayChannel() == ExportChannel.Contrast;
                ComboBoxHelper.SelectItemByTag(cbDisplayChannel, ExportChannel.Contrast.ToString());
                if (refreshAfterSelection && GetSelectedDisplayChannel() == ExportChannel.Contrast && HasXyzData())
                {
                    RefreshDisplayedImage();
                    UpdateReferencePlot();
                }
            }
            catch (Exception ex)
            {
                log.Error($"计算对比度失败: {ex.Message}", ex);
                MessageBox.Show(ex.Message, "对比度", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
