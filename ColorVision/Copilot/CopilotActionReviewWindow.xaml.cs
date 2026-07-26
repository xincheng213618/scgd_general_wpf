using System;
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
            InitializeComponent();
            DataContext = action;
            ApproveButton.IsEnabled = !action.HasReviewDetails;
            Loaded += CopilotActionReviewWindow_Loaded;
        }

        private void CopilotActionReviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyOwnerThemeResources();
            ReviewDetailsTextBox.Focus();
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
