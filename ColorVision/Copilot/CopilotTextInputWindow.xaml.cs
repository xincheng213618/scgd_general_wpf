using LocalizedText = global::ColorVision.DisplayText;
using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public partial class CopilotTextInputWindow : Window
    {
        internal static readonly RoutedCommand ConfirmInputCommand = new(nameof(ConfirmInputCommand), typeof(CopilotTextInputWindow));
        private readonly int _maximumLength;
        private readonly int _initialCaretIndex;

        public CopilotTextInputWindow(
            string title,
            string description,
            string initialText = "",
            bool isMultiline = false,
            bool isReadOnly = false,
            int maximumLength = 0,
            int initialCaretIndex = -1,
            bool acceptsTab = false)
        {
            InitializeComponent();
            this.ApplyCaption();

            _maximumLength = Math.Max(0, maximumLength);
            _initialCaretIndex = initialCaretIndex;
            Title = title;
            DescriptionTextBlock.Text = description;
            InputTextBox.MaxLength = _maximumLength;
            InputTextBox.Text = initialText;
            InputTextBox.AcceptsReturn = isMultiline;
            InputTextBox.AcceptsTab = acceptsTab;
            InputTextBox.IsReadOnly = isReadOnly;
            InputTextBox.MinHeight = isMultiline ? 140 : 38;
            InputTextBox.VerticalContentAlignment = isMultiline ? VerticalAlignment.Top : VerticalAlignment.Center;
            Height = isMultiline ? 320 : 190;
            if (isReadOnly)
            {
                CancelButton.Visibility = Visibility.Collapsed;
                OkButton.Content = LocalizedText.Get("关闭");
            }
            else if (_maximumLength > 0)
            {
                CharacterCountTextBlock.Visibility = Visibility.Visible;
                InputTextBox.TextChanged += InputTextBox_TextChanged;
                UpdateCharacterCount();
            }

            Loaded += CopilotTextInputWindow_Loaded;
            PreviewKeyDown += CopilotTextInputWindow_PreviewKeyDown;
            CommandBindings.Add(new CommandBinding(ConfirmInputCommand, (_, e) =>
            {
                ConfirmInput();
                e.Handled = true;
            }, (_, e) =>
            {
                e.CanExecute = true;
                e.Handled = true;
            }));
            if (isMultiline)
                InputBindings.Add(new KeyBinding(ConfirmInputCommand, Key.Enter, ModifierKeys.Control));
        }

        internal Func<bool>? TryAcceptInput { get; set; }

        public string ResultText => InputTextBox.Text.Trim();

        public string RawResultText => InputTextBox.Text;

        public int ResultCaretIndex => InputTextBox.CaretIndex;

        internal void ShowAcceptanceFeedback(string message, string? currentText)
        {
            AcceptanceFeedbackTextBlock.Text = LocalizedText.Get(message);
            AcceptanceFeedbackPanel.Visibility = Visibility.Visible;
            ComparisonLabelTextBlock.Text = LocalizedText.Get("当前输入框草稿（只读，可复制）");
            ComparisonLabelTextBlock.Visibility = currentText == null ? Visibility.Collapsed : Visibility.Visible;
            ComparisonTextBox.Text = currentText ?? string.Empty;
            ComparisonTextBox.Visibility = currentText == null ? Visibility.Collapsed : Visibility.Visible;
            if (currentText != null)
                Height = Math.Max(Height, Math.Min(640, SystemParameters.WorkArea.Height));
            InputTextBox.Focus();
        }

        private void ConfirmInput()
        {
            if (TryAcceptInput?.Invoke() != false)
                DialogResult = true;
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateCharacterCount();

        private void UpdateCharacterCount()
        {
            CharacterCountTextBlock.Text = $"{InputTextBox.Text.Length:N0} / {_maximumLength:N0}";
        }

        private void CopilotTextInputWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InputTextBox.Focus();
            InputTextBox.CaretIndex = _initialCaretIndex < 0
                ? InputTextBox.Text.Length
                : Math.Clamp(_initialCaretIndex, 0, InputTextBox.Text.Length);
        }

        private void CopilotTextInputWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (InputTextBox.IsReadOnly && e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = true;
                return;
            }

            if (!InputTextBox.IsReadOnly && e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
                return;
            }

            if (!InputTextBox.AcceptsReturn && e.Key == Key.Enter)
            {
                e.Handled = true;
                ConfirmInput();
                return;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ConfirmInput();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
