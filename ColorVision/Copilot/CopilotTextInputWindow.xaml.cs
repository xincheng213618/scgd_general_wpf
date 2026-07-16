using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public partial class CopilotTextInputWindow : Window
    {
        private readonly int _maximumLength;

        public CopilotTextInputWindow(
            string title,
            string description,
            string initialText = "",
            bool isMultiline = false,
            bool isReadOnly = false,
            int maximumLength = 0)
        {
            InitializeComponent();
            this.ApplyCaption();

            _maximumLength = Math.Max(0, maximumLength);
            Title = title;
            DescriptionTextBlock.Text = description;
            InputTextBox.Text = initialText;
            InputTextBox.MaxLength = _maximumLength;
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
            else if (_maximumLength > 0)
            {
                CharacterCountTextBlock.Visibility = Visibility.Visible;
                InputTextBox.TextChanged += InputTextBox_TextChanged;
                UpdateCharacterCount();
            }

            Loaded += CopilotTextInputWindow_Loaded;
            PreviewKeyDown += CopilotTextInputWindow_PreviewKeyDown;
        }

        public string ResultText => InputTextBox.Text.Trim();

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateCharacterCount();

        private void UpdateCharacterCount()
        {
            CharacterCountTextBlock.Text = $"{InputTextBox.Text.Length:N0} / {_maximumLength:N0}";
        }

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
