using System;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Copilot
{
    internal partial class CopilotActionReviewWindow : Window
    {
        private static readonly string[] ThemeResourceKeys =
        {
            "GlobalBackground",
            "GlobalBorderBrush",
            "GlobalBorderBrush1",
            "GlobalTextBrush",
            "SecondaryTextBrush",
            "ButtonBackground",
            "ButtonBorderBrush",
            "PrimaryBrush",
        };

        private readonly Mcp.ConfirmableAction _action;

        internal CopilotActionReviewWindow(Mcp.ConfirmableAction action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            DataContext = action;
            InitializeComponent();
            ApproveButton.IsEnabled = action.IsPending && !action.HasReviewDetails;
            _action.PropertyChanged += Action_PropertyChanged;
            Closed += CopilotActionReviewWindow_Closed;
            Loaded += CopilotActionReviewWindow_Loaded;
        }

        private void CopilotActionReviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyOwnerThemeResources();
            if (!_action.IsPending)
            {
                Close();
                return;
            }
            ReviewDetailsTextBox.Focus();
        }

        private void Action_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Mcp.ConfirmableAction.Status))
                return;

            if (Dispatcher.CheckAccess())
                InvalidateTerminalReview();
            else
                _ = Dispatcher.BeginInvoke(InvalidateTerminalReview);
        }

        private void InvalidateTerminalReview()
        {
            if (_action.IsPending)
                return;

            ApproveButton.IsEnabled = false;
            ReviewAcknowledgementCheckBox.IsEnabled = false;
            if (IsVisible)
                Close();
        }

        private void CopilotActionReviewWindow_Closed(object? sender, EventArgs e)
        {
            _action.PropertyChanged -= Action_PropertyChanged;
        }

        private void ApplyOwnerThemeResources()
        {
            if (Owner == null)
                return;

            foreach (var key in ThemeResourceKeys)
            {
                var value = Owner.TryFindResource(key);
                if (value != null)
                    Resources[key] = value;
            }
        }

        private void ReviewAcknowledgementCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ApproveButton.IsEnabled = !_action.HasReviewDetails
                || ReviewAcknowledgementCheckBox.IsChecked == true;
        }

        private void ApproveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_action.HasReviewDetails && ReviewAcknowledgementCheckBox.IsChecked != true)
                return;

            DialogResult = true;
        }
    }
}
