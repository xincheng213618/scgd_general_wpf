using System;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Copilot
{
    public enum CopilotSettingsPage
    {
        Models,
        Agent,
        Mcp,
    }

    public partial class CopilotSettingsWindow : Window
    {
        public CopilotSettingsWindow(CopilotSettingsPage initialPage = CopilotSettingsPage.Models)
        {
            InitializeComponent();
            this.ApplyCaption();
            DataContext = new CopilotSettingsViewModel();
            SettingsTabs.SelectedIndex = (int)initialPage;
        }

        public bool HasAppliedChanges => ViewModel.HasAppliedChanges;

        public string ActiveProfileId => ViewModel.ActiveProfileId;

        private CopilotSettingsViewModel ViewModel => (CopilotSettingsViewModel)DataContext;

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Save();
        }

        private void OpenAddModelButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PrepareAddModelDialog();
            var window = new CopilotAddModelWindow(ViewModel)
            {
                Owner = this,
            };

            window.ShowDialog();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.Save())
                return;

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is CopilotSettingsViewModel viewModel)
                viewModel.Dispose();
            base.OnClosed(e);
        }
    }

    public static class PasswordBoxBinding
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword",
                typeof(string),
                typeof(PasswordBoxBinding),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnBoundPasswordChanged));

        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BindPassword",
                typeof(bool),
                typeof(PasswordBoxBinding),
                new PropertyMetadata(false, OnBindPasswordChanged));

        private static readonly DependencyProperty UpdatingPasswordProperty =
            DependencyProperty.RegisterAttached(
                "UpdatingPassword",
                typeof(bool),
                typeof(PasswordBoxBinding),
                new PropertyMetadata(false));

        public static string GetBoundPassword(DependencyObject dependencyObject) =>
            (string)dependencyObject.GetValue(BoundPasswordProperty);

        public static void SetBoundPassword(DependencyObject dependencyObject, string value) =>
            dependencyObject.SetValue(BoundPasswordProperty, value ?? string.Empty);

        public static bool GetBindPassword(DependencyObject dependencyObject) =>
            (bool)dependencyObject.GetValue(BindPasswordProperty);

        public static void SetBindPassword(DependencyObject dependencyObject, bool value) =>
            dependencyObject.SetValue(BindPasswordProperty, value);

        private static bool GetUpdatingPassword(DependencyObject dependencyObject) =>
            (bool)dependencyObject.GetValue(UpdatingPasswordProperty);

        private static void SetUpdatingPassword(DependencyObject dependencyObject, bool value) =>
            dependencyObject.SetValue(UpdatingPasswordProperty, value);

        private static void OnBindPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is not PasswordBox passwordBox)
                return;

            if ((bool)e.OldValue)
                passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;

            if ((bool)e.NewValue)
                passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
        }

        private static void OnBoundPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is not PasswordBox passwordBox)
                return;

            if (!GetBindPassword(passwordBox) || GetUpdatingPassword(passwordBox))
                return;

            var password = e.NewValue as string ?? string.Empty;
            if (passwordBox.Password == password)
                return;

            passwordBox.Password = password;
        }

        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox passwordBox)
                return;

            SetUpdatingPassword(passwordBox, true);
            SetBoundPassword(passwordBox, passwordBox.Password);
            SetUpdatingPassword(passwordBox, false);
        }
    }
}
