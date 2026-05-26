using Conoscope.Analysis;
using Conoscope.ApplicationServices.Analysis;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Conoscope
{
    public partial class ConoscopeWindow
    {
        private readonly ConoscopeAnalysisWorkflow analysisWorkflow = new();
        private readonly Dictionary<Button, RecordButtonVisualState> recordButtonVisualStates = new();

        private sealed class RecordButtonVisualState
        {
            public RecordButtonVisualState(object? content, object? toolTip)
            {
                Content = content;
                ToolTip = toolTip;
            }

            public object? Content { get; }
            public object? ToolTip { get; }
        }

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

            btnComputeGamut.IsEnabled = analysisWorkflow.CanComputeGamut(cbRibbonGamutStandard?.SelectedItem as ColorGamutStandard);
            btnClearGamut.IsEnabled = analysisWorkflow.HasAnyGamutCapture;
            btnComputeContrast.IsEnabled = analysisWorkflow.CanComputeContrast;
            btnClearContrast.IsEnabled = analysisWorkflow.HasAnyContrastCapture;

            UpdateRecordButton(btnRecordGamutRed, analysisWorkflow.GamutRedCapture, Color.FromRgb(214, 69, 65), "R");
            UpdateRecordButton(btnRecordGamutGreen, analysisWorkflow.GamutGreenCapture, Color.FromRgb(66, 165, 79), "G");
            UpdateRecordButton(btnRecordGamutBlue, analysisWorkflow.GamutBlueCapture, Color.FromRgb(52, 120, 246), "B");
            UpdateRecordButton(btnRecordContrastWhite, analysisWorkflow.ContrastWhiteCapture, Color.FromRgb(160, 160, 160), Properties.Resources.SlotWhite);
            UpdateRecordButton(btnRecordContrastBlack, analysisWorkflow.ContrastBlackCapture, Color.FromRgb(90, 90, 90), Properties.Resources.SlotBlack);
        }

        private RecordButtonVisualState GetRecordButtonVisualState(Button button)
        {
            if (recordButtonVisualStates.TryGetValue(button, out RecordButtonVisualState? state))
            {
                return state;
            }

            state = new RecordButtonVisualState(button.Content, button.ToolTip);
            recordButtonVisualStates.Add(button, state);
            return state;
        }

        private void UpdateRecordButton(Button button, MeasurementCapture? capture, Color accentColor, string slotName)
        {
            RecordButtonVisualState baseState = GetRecordButtonVisualState(button);
            button.Content = baseState.Content;

            if (capture == null)
            {
                button.ClearValue(Control.BackgroundProperty);
                button.ClearValue(Control.BorderBrushProperty);
                button.ClearValue(Control.ForegroundProperty);
                button.ClearValue(Control.FontWeightProperty);
                button.ToolTip = baseState.ToolTip;
                return;
            }

            button.Background = new SolidColorBrush(Color.FromArgb(64, accentColor.R, accentColor.G, accentColor.B));
            button.BorderBrush = new SolidColorBrush(accentColor);
            button.Foreground = Brushes.White;
            button.FontWeight = FontWeights.SemiBold;
            button.ToolTip = Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgSlotRecordedDetail, slotName, capture.SourceDisplayName, capture.PointCount);
        }

        private void btnRecordGamutRed_Click(object sender, RoutedEventArgs e) => RecordFocusCapture(CaptureSlot.GamutRed, "R", Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgRecordedRGamut, "R"));
        private void btnRecordGamutGreen_Click(object sender, RoutedEventArgs e) => RecordFocusCapture(CaptureSlot.GamutGreen, "G", Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgRecordedRGamut, "G"));
        private void btnRecordGamutBlue_Click(object sender, RoutedEventArgs e) => RecordFocusCapture(CaptureSlot.GamutBlue, "B", Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgRecordedRGamut, "B"));

        private void btnClearGamut_Click(object sender, RoutedEventArgs e)
        {
            analysisWorkflow.ClearGamut();
            RefreshAnalysisRibbonState(ActiveView);
            SetOperationStatus(Properties.Resources.MsgClearedGamut, Brushes.Gray);
        }

        private void btnComputeGamut_Click(object sender, RoutedEventArgs e)
        {
            if (cbRibbonGamutStandard.SelectedItem is not ColorGamutStandard standard)
            {
                MessageBox.Show(this, Properties.Resources.MsgSelectGamutStandard, Properties.Resources.TitleGamutCalc, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AnalysisWorkflowResult<ColorGamutComputationResult> result = analysisWorkflow.ComputeGamut(standard);
            if (!result.IsSuccess)
            {
                MessageBox.Show(this, result.ErrorMessage, Properties.Resources.TitleGamutCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                SetOperationStatus(Properties.Resources.MsgGamutFailed, Brushes.OrangeRed);
                return;
            }

            ColorGamutResultWindow window = new(result.Value!)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
            window.Activate();
            SetOperationStatus(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgGamutComputed, standard.Name), Brushes.LimeGreen);
        }

        private void btnRecordContrastWhite_Click(object sender, RoutedEventArgs e) => RecordFocusCapture(CaptureSlot.ContrastWhite, Properties.Resources.SlotWhite, Properties.Resources.MsgRecordedWhite);
        private void btnRecordContrastBlack_Click(object sender, RoutedEventArgs e) => RecordFocusCapture(CaptureSlot.ContrastBlack, Properties.Resources.SlotBlack, Properties.Resources.MsgRecordedBlack);

        private void btnClearContrast_Click(object sender, RoutedEventArgs e)
        {
            analysisWorkflow.ClearContrast();
            RefreshAnalysisRibbonState(ActiveView);
            SetOperationStatus(Properties.Resources.MsgClearedContrast, Brushes.Gray);
        }

        private void btnComputeContrast_Click(object sender, RoutedEventArgs e)
        {
            AnalysisWorkflowResult<ContrastComputationResult> result = analysisWorkflow.ComputeContrast();
            if (!result.IsSuccess)
            {
                MessageBox.Show(this, result.ErrorMessage, Properties.Resources.TitleContrastCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                SetOperationStatus(Properties.Resources.MsgContrastFailed, Brushes.OrangeRed);
                return;
            }

            ContrastResultWindow window = new(result.Value!)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
            window.Activate();
            SetOperationStatus(Properties.Resources.MsgContrastComputed, Brushes.LimeGreen);
        }

        private void RecordFocusCapture(CaptureSlot slot, string slotName, string successMessage)
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

            analysisWorkflow.RecordCapture(slot, capture);
            RefreshAnalysisRibbonState(activeView);
            SetOperationStatus(successMessage, Brushes.LimeGreen);
        }
    }
}
