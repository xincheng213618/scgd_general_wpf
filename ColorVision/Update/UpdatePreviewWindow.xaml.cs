using ColorVision.Themes;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public partial class UpdatePreviewWindow
    {
        private readonly Func<UpdatePreviewWindow, Task>? _initializeAsync;
        private bool _hasInitialized;

        public UpdatePreviewAction ResultAction { get; private set; } = UpdatePreviewAction.None;

        public UpdatePreviewDialogContext Context { get; }

        public Task InitializationTask { get; private set; } = Task.CompletedTask;

        public bool IsClosed { get; private set; }

        public bool SuppressPostCheckMessage { get; private set; }

        public UpdatePreviewWindow(UpdatePreviewDialogContext context, Func<UpdatePreviewWindow, Task>? initializeAsync = null)
        {
            Context = context;
            _initializeAsync = initializeAsync;
            DataContext = Context;

            InitializeComponent();
            this.ApplyCaption();

            ContentRendered += UpdatePreviewWindow_ContentRendered;
            Closing += (_, _) =>
            {
                if (Context.IsChecking)
                {
                    SuppressPostCheckMessage = true;
                }
            };
            Closed += (_, _) =>
            {
                IsClosed = true;
            };
        }

        private async void UpdatePreviewWindow_ContentRendered(object? sender, EventArgs e)
        {
            if (_hasInitialized || _initializeAsync == null)
                return;

            _hasInitialized = true;
            InitializationTask = _initializeAsync(this);

            try
            {
                await InitializationTask;
            }
            catch
            {
                if (!IsClosed)
                {
                    DialogResult = false;
                }
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Context.CanConfirm)
                return;

            ResultAction = UpdatePreviewAction.UpdateNow;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Context.CanCancel)
                return;

            if (Context.IsChecking)
            {
                SuppressPostCheckMessage = true;
            }

            ResultAction = UpdatePreviewAction.None;
            DialogResult = false;
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            ResultAction = UpdatePreviewAction.SkipVersion;
            DialogResult = false;
        }
    }
}