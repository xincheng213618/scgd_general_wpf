using Conoscope.Analysis;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Conoscope
{
    public partial class ConoscopeWindow
    {
        private readonly DefaultBatchColorGamutCalculator batchColorGamutCalculator = new();
        private readonly DefaultBatchContrastCalculator batchContrastCalculator = new();

        private MeasurementCapture? gamutRedCapture;
        private MeasurementCapture? gamutGreenCapture;
        private MeasurementCapture? gamutBlueCapture;
        private MeasurementCapture? contrastWhiteCapture;
        private MeasurementCapture? contrastBlackCapture;

        private void InitializeAnalysisRibbonControls()
        {
            if (cbRibbonGamutStandard == null)
            {
                return;
            }

            cbRibbonGamutStandard.ItemsSource = ColorGamutStandards.All;
            ColorGamutStandard? selectedStandard = null;
            for (int index = 0; index < ColorGamutStandards.All.Count; index++)
            {
                ColorGamutStandard standard = ColorGamutStandards.All[index];
                if (string.Equals(standard.Name, "sRGB", StringComparison.OrdinalIgnoreCase))
                {
                    selectedStandard = standard;
                    break;
                }
            }

            cbRibbonGamutStandard.SelectedItem = selectedStandard ?? (ColorGamutStandards.All.Count > 0 ? ColorGamutStandards.All[0] : null);
            RefreshAnalysisRibbonState(ActiveView);
        }

        private void RefreshAnalysisRibbonState(ConoscopeView? activeView)
        {
            bool hasActiveView = activeView != null;

            if (btnRecordGamutRed == null)
            {
                return;
            }

            btnRecordGamutRed.IsEnabled = hasActiveView;
            btnRecordGamutGreen.IsEnabled = hasActiveView;
            btnRecordGamutBlue.IsEnabled = hasActiveView;
            btnRecordContrastWhite.IsEnabled = hasActiveView;
            btnRecordContrastBlack.IsEnabled = hasActiveView;

            btnComputeGamut.IsEnabled = gamutRedCapture != null && gamutGreenCapture != null && gamutBlueCapture != null && cbRibbonGamutStandard?.SelectedItem is ColorGamutStandard;
            btnClearGamut.IsEnabled = gamutRedCapture != null || gamutGreenCapture != null || gamutBlueCapture != null;
            btnComputeContrast.IsEnabled = contrastWhiteCapture != null && contrastBlackCapture != null;
            btnClearContrast.IsEnabled = contrastWhiteCapture != null || contrastBlackCapture != null;

            UpdateRecordButton(btnRecordGamutRed, gamutRedCapture, Color.FromRgb(214, 69, 65), "R");
            UpdateRecordButton(btnRecordGamutGreen, gamutGreenCapture, Color.FromRgb(66, 165, 79), "G");
            UpdateRecordButton(btnRecordGamutBlue, gamutBlueCapture, Color.FromRgb(52, 120, 246), "B");
            UpdateRecordButton(btnRecordContrastWhite, contrastWhiteCapture, Color.FromRgb(160, 160, 160), "白");
            UpdateRecordButton(btnRecordContrastBlack, contrastBlackCapture, Color.FromRgb(90, 90, 90), "黑");
        }

        private static void UpdateRecordButton(Button button, MeasurementCapture? capture, Color accentColor, string slotName)
        {
            if (capture == null)
            {
                button.Content = $"记录 {slotName}";
                button.ClearValue(Control.BackgroundProperty);
                button.ClearValue(Control.BorderBrushProperty);
                button.ClearValue(Control.ForegroundProperty);
                button.ClearValue(Control.FontWeightProperty);
                button.ToolTip = $"从当前活动 View 的全部关注点记录 {slotName} 数据";
                return;
            }

            button.Content = $"已记录 {slotName}";
            button.Background = new SolidColorBrush(Color.FromArgb(64, accentColor.R, accentColor.G, accentColor.B));
            button.BorderBrush = new SolidColorBrush(accentColor);
            button.Foreground = Brushes.White;
            button.FontWeight = FontWeights.SemiBold;
            button.ToolTip = $"{slotName} 已记录\n来源: {capture.SourceDisplayName}\n数量: {capture.PointCount}";
        }

        private void btnRecordGamutRed_Click(object sender, RoutedEventArgs e)
        {
            RecordFocusCapture("R", capture => gamutRedCapture = capture, "已记录 R 关注点数据");
        }

        private void btnRecordGamutGreen_Click(object sender, RoutedEventArgs e)
        {
            RecordFocusCapture("G", capture => gamutGreenCapture = capture, "已记录 G 关注点数据");
        }

        private void btnRecordGamutBlue_Click(object sender, RoutedEventArgs e)
        {
            RecordFocusCapture("B", capture => gamutBlueCapture = capture, "已记录 B 关注点数据");
        }

        private void btnClearGamut_Click(object sender, RoutedEventArgs e)
        {
            gamutRedCapture = null;
            gamutGreenCapture = null;
            gamutBlueCapture = null;
            RefreshAnalysisRibbonState(ActiveView);
            SetOperationStatus("已清空色域记录", Brushes.Gray);
        }

        private void btnComputeGamut_Click(object sender, RoutedEventArgs e)
        {
            if (gamutRedCapture == null || gamutGreenCapture == null || gamutBlueCapture == null)
            {
                MessageBox.Show(this, "请先记录或导入 R/G/B 三组数据。", "色域计算", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (cbRibbonGamutStandard.SelectedItem is not ColorGamutStandard standard)
            {
                MessageBox.Show(this, "请选择色域标准。", "色域计算", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                ColorGamutComputationResult result = batchColorGamutCalculator.Calculate(gamutRedCapture, gamutGreenCapture, gamutBlueCapture, standard);
                ColorGamutResultWindow window = new(result)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.Show();
                window.Activate();
                SetOperationStatus($"已完成 {standard.Name} 色域计算", Brushes.LimeGreen);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "色域计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetOperationStatus("色域计算失败", Brushes.OrangeRed);
            }
        }

        private void btnRecordContrastWhite_Click(object sender, RoutedEventArgs e)
        {
            RecordFocusCapture("白", capture => contrastWhiteCapture = capture, "已记录白场关注点数据");
        }

        private void btnRecordContrastBlack_Click(object sender, RoutedEventArgs e)
        {
            RecordFocusCapture("黑", capture => contrastBlackCapture = capture, "已记录黑场关注点数据");
        }

        private void btnClearContrast_Click(object sender, RoutedEventArgs e)
        {
            contrastWhiteCapture = null;
            contrastBlackCapture = null;
            RefreshAnalysisRibbonState(ActiveView);
            SetOperationStatus("已清空对比度记录", Brushes.Gray);
        }

        private void btnComputeContrast_Click(object sender, RoutedEventArgs e)
        {
            if (contrastWhiteCapture == null || contrastBlackCapture == null)
            {
                MessageBox.Show(this, "请先记录白/黑两组数据。", "对比度计算", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                ContrastComputationResult result = batchContrastCalculator.Calculate(contrastWhiteCapture, contrastBlackCapture);
                ContrastResultWindow window = new(result)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.Show();
                window.Activate();
                SetOperationStatus("已完成对比度计算", Brushes.LimeGreen);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "对比度计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetOperationStatus("对比度计算失败", Brushes.OrangeRed);
            }
        }

        private void RecordFocusCapture(string slotName, Action<MeasurementCapture> applyCapture, string successMessage)
        {
            ConoscopeView? activeView = ActiveView;
            if (activeView == null)
            {
                MessageBox.Show(this, "当前没有活动的 Conoscope 视图。", "分析记录", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!activeView.TryGetFocusPointMeasurementCapture(slotName, out MeasurementCapture capture, out string? errorMessage))
            {
                MessageBox.Show(this, errorMessage ?? "当前关注点不可用。", "分析记录", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            applyCapture(capture);
            RefreshAnalysisRibbonState(activeView);
            SetOperationStatus(successMessage, Brushes.LimeGreen);
        }
    }
}