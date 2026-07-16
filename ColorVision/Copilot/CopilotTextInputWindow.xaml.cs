using ColorVision.Themes;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public partial class CopilotTextInputWindow : Window
    {
        public CopilotTextInputWindow(string title, string description, string initialText = "", bool isMultiline = false, bool isReadOnly = false)
        {
            InitializeComponent();
            this.ApplyCaption();

            Title = title;
            DescriptionTextBlock.Text = description;
            InputTextBox.Text = initialText;
            InputTextBox.AcceptsReturn = isMultiline;
            InputTextBox.IsReadOnly = isReadOnly;
            InputTextBox.MinHeight = isMultiline ? 140 : 38;
            InputTextBox.VerticalContentAlignment = isMultiline ? VerticalAlignment.Top : VerticalAlignment.Center;
            Height = isMultiline ? 320 : 190;
            if (isReadOnly)
            {
                CancelButton.Visibility = Visibility.Collapsed;
                OkButton.Content = "关闭";
            }

            Loaded += CopilotTextInputWindow_Loaded;
            PreviewKeyDown += CopilotTextInputWindow_PreviewKeyDown;
        }

        public string ResultText => InputTextBox.Text.Trim();

        private void CopilotTextInputWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InputTextBox.Focus();
            InputTextBox.CaretIndex = InputTextBox.Text.Length;
        }

        private void CopilotTextInputWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (InputTextBox.IsReadOnly && e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = true;
                return;
            }

            if (!InputTextBox.AcceptsReturn && e.Key == Key.Enter)
            {
                e.Handled = true;
                DialogResult = true;
                return;
            }

            if (InputTextBox.AcceptsReturn && e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                DialogResult = true;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
