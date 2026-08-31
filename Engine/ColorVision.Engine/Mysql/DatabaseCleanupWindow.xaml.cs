using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Database
{
    public partial class DatabaseCleanupWindow : Window
    {
        private static readonly Dictionary<string, DatabaseCleanupWindow> Instances = new(StringComparer.OrdinalIgnoreCase);
        private readonly DatabaseCleanupWindowViewModel _viewModel;
        private readonly bool _refreshOnLoad;
        private bool _hasLoaded;

        public DatabaseCleanupWindow() : this(new DatabaseCleanupWindowViewModel(), refreshOnLoad: true)
        {
        }

        internal DatabaseCleanupWindow(DatabaseCleanupWindowViewModel viewModel, bool refreshOnLoad = false)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            _viewModel = viewModel;
            _refreshOnLoad = refreshOnLoad;
            InitializeComponent();
            DataContext = viewModel;
            this.ApplyCaption();
        }

        public static void OpenWindow(Window? owner = null, IDatabaseCleanupSourceProvider? source = null)
        {
            if (source != null)
                ArgumentException.ThrowIfNullOrWhiteSpace(source.Id);

            string scopeKey = source == null ? "global" : $"source:{source.Id}";
            if (Instances.TryGetValue(scopeKey, out DatabaseCleanupWindow? existingWindow))
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                    existingWindow.WindowState = WindowState.Normal;

                existingWindow.Activate();
                return;
            }

            owner ??= WindowHelpers.GetActiveWindow();
            var window = new DatabaseCleanupWindow(CreateViewModel(source), refreshOnLoad: true)
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            };
            Instances.Add(scopeKey, window);
            window.Closed += (_, _) => Instances.Remove(scopeKey);
            window.Show();
        }

        internal static DatabaseCleanupWindowViewModel CreateViewModel(IDatabaseCleanupSourceProvider? source)
        {
            return source == null
                ? new DatabaseCleanupWindowViewModel()
                : new DatabaseCleanupWindowViewModel([source], isSourceScoped: true);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_hasLoaded)
                return;

            _hasLoaded = true;
            if (_refreshOnLoad)
                await _viewModel.RefreshAllAsync();
        }

        private void CleanupTablesGrid_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTableSelectionColumn(sender as DataGrid);
        }

        private void CleanupTablesGrid_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateTableSelectionColumn(sender as DataGrid);
        }

        private static void UpdateTableSelectionColumn(DataGrid? grid)
        {
            if (grid?.Columns.Count > 0)
            {
                grid.Columns[0].Visibility = grid.DataContext is DatabaseCleanupSourceViewModel { SupportsTableCleanup: true }
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }
    }
}
