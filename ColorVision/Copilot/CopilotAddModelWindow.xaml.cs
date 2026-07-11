using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public partial class CopilotAddModelWindow : Window
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

        private bool _committed;
        private readonly ThemeChangedHandler _themeChangedHandler;

        public CopilotAddModelWindow(CopilotSettingsViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            InitializeComponent();
            DataContext = viewModel;
            _themeChangedHandler = _ => Dispatcher.BeginInvoke(ApplyOwnerThemeResources);
            ThemeManager.Current.CurrentUIThemeChanged += _themeChangedHandler;
            Loaded += CopilotAddModelWindow_Loaded;
            Closed += CopilotAddModelWindow_Closed;
        }

        private CopilotSettingsViewModel ViewModel => (CopilotSettingsViewModel)DataContext;

        private void CopilotAddModelWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyOwnerThemeResources();
            SearchTextBox.Focus();
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

        private void WindowChrome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
                return;

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void ConnectPageGrid_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ConnectPageGrid.IsVisible)
                ApiKeyPasswordBox.Focus();
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.AddQuickProfile(useNow: true))
            {
                _committed = true;
                DialogResult = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearQuickAddModelDraft();
            DialogResult = false;
        }

        private void CopilotAddModelWindow_Closed(object? sender, EventArgs e)
        {
            ThemeManager.Current.CurrentUIThemeChanged -= _themeChangedHandler;
            if (!_committed)
                ViewModel.ClearQuickAddModelDraft();
        }
    }
}
