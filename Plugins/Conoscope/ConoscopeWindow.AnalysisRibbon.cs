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
            UpdateRecordButton(btnRecordContrastWhite, contrastWhiteCapture, Color.FromRgb(160, 160, 160), Properties.Resources.SlotWhite);
            UpdateRecordButton(btnRecordContrastBlack, contrastBlackCapture, Color.FromRgb(90, 90, 90), Properties.Resources.SlotBlack);
        }

        private static void UpdateRecordButton(Button button, MeasurementCapture? capture, Color accentColor, string slotName)
        {
            if (capture == null)
            {
                button.Content = string.Format(Properties.Resources.MsgRecordSlot, slotName);
                button.ClearValue(Control.BackgroundProperty);
                button.ClearValue(Control.BorderBrushProperty);
                button.ClearValue(Control.ForegroundProperty);
                button.ClearValue(Control.FontWeightProperty);
                button.ClearValue(FrameworkElement.ToolTipProperty);
                return;
            }

            button.Content = string.Format(Properties.Resources.MsgRecordedSlot, slotName);
            button.Background = new SolidColorBrush(Color.FromArgb(64, accentColor.R, accentColor.G, accentColor.B));
            button.BorderBrush = new SolidColorBrush(accentColor);
            button.Foreground = Brushes.White;
            button.FontWeight = FontWeights.SemiBold;
            button.ToolTip = string.Format(Properties.Resources.MsgSlotRecordedDetail, slotName, capture.SourceDisplayName, capture.PointCount);
        }

        private void btnRecordGamutRed_Click(object sender, RoutedEventArgs e)
        {
            RecordFocusCapture("R", capture => gamutRedCapture = capture, string.Format(Properties.Resources.MsgRecordedRGamut, "R"));
        }

        private void btnRecordGamutGreen_Click(object sender, RoutedEventArgs e)
        {
            RecordFocusCapture("G", capture => gamutGreenCapture = capture, string.Format(Properties.Resources.MsgRecordedRGamut, "G"));
        }

        private void btnRecordGamutBlue_Click(object sender, RoutedEventArgs e)
        {
            RecordFocusCapture("B", capture => gamutBlueCapture = capture, string.Format(Properties.Resources.MsgRecordedRGamut, "B"));
        }

        private void btnClearGamut_Click(object sender, RoutedEventArgs e)
        {
            gamutRedCapture = null;
            gamutGreenCapture = null;
            gamutBlueCapture = null;
            RefreshAnalysisRibbonState(ActiveView);
            SetOperationStatus(Properties.Resources.MsgClearedGamut, Brushes.Gray);
        }

        private void btnComputeGamut_Click(object sender, RoutedEventArgs e)
        {
            if (gamutRedCapture == null || gamutGreenCapture == null || gamutBlueCapture == null)
            {
                MessageBox.Show(this, Properties.Resources.MsgNeedRGBData, Properties.Resources.TitleGamutCalc, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (cbRibbonGamutStandard.SelectedItem is not ColorGamutStandard standard)
            {
                MessageBox.Show(this, Properties.Resources.MsgSelectGamutStandard, Properties.Resources.TitleGamutCalc, MessageBoxButton.OK, MessageBoxImage.Information);
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
                SetOperationStatus(string.Format(Properties.Resources.MsgGamutComputed, standard.Name), Brushes.LimeGreen);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.TitleGamutCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                SetOperationStatus(Properties.Resources.MsgGamutFailed, Brushes.OrangeRed);
            }
        }

        private void btnRecordContrastWhite_Click(object sender, RoutedEventArgs e)
        {
            RecordFocusCapture(Properties.Resources.SlotWhite, capture => contrastWhiteCapture = capture, Properties.Resources.MsgRecordedWhite);
        }

        private void btnRecordContrastBlack_Click(object sender, RoutedEventArgs e)
        {
            RecordFocusCapture(Properties.Resources.SlotBlack, capture => contrastBlackCapture = capture, Properties.Resources.MsgRecordedBlack);
        }

        private void btnClearContrast_Click(object sender, RoutedEventArgs e)
        {
            contrastWhiteCapture = null;
            contrastBlackCapture = null;
            RefreshAnalysisRibbonState(ActiveView);
            SetOperationStatus(Properties.Resources.MsgClearedContrast, Brushes.Gray);
        }

        private void btnComputeContrast_Click(object sender, RoutedEventArgs e)
        {
            if (contrastWhiteCapture == null || contrastBlackCapture == null)
            {
                MessageBox.Show(this, Properties.Resources.MsgNeedWhiteBlackData, Properties.Resources.TitleContrastCalc, MessageBoxButton.OK, MessageBoxImage.Information);
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
                SetOperationStatus(Properties.Resources.MsgContrastComputed, Brushes.LimeGreen);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.TitleContrastCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                SetOperationStatus(Properties.Resources.MsgContrastFailed, Brushes.OrangeRed);
            }
        }

        private void RecordFocusCapture(string slotName, Action<MeasurementCapture> applyCapture, string successMessage)
        {
            ConoscopeView? activeView = ActiveView;
            if (activeView == null)
            {
                MessageBox.Show(this, Properties.Resources.MsgNoActiveView, Properties.Resources.TitleAnalysisRecord, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!activeView.TryGetFocusPointMeasurementCapture(slotName, out MeasurementCapture capture, out string? errorMessage))
            {
                MessageBox.Show(this, errorMessage ?? Properties.Resources.MsgFocusPointsUnavailable, Properties.Resources.TitleAnalysisRecord, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            applyCapture(capture);
            RefreshAnalysisRibbonState(activeView);
            SetOperationStatus(successMessage, Brushes.LimeGreen);
        }
    }
}
