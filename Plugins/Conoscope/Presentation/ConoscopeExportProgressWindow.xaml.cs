using Conoscope.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Conoscope.Presentation
{
    internal sealed record ConoscopeExportJob(string Path, Action<string, ConoscopeExportContext> Export);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Window close and worker completion jointly own cancellation and Mat disposal; disposal cannot precede the worker.")]
    public partial class ConoscopeExportProgressWindow : Window
    {
        private readonly CancellationTokenSource cancellation = new();
        private readonly IReadOnlyList<ConoscopeExportJob> jobs;
        private readonly ConoscopeExportSource source;
        private bool closed;
        private bool finished;
        private bool started;
        private Task? execution;
        internal int CompletedFiles { get; private set; }
        internal Exception? Error { get; private set; }

        internal ConoscopeExportProgressWindow(ConoscopeExportSource source, IReadOnlyList<ConoscopeExportJob> jobs)
        {
            this.source = source;
            this.jobs = jobs;
            InitializeComponent();
            ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
            StatusText.Text = Properties.Resources.ExportRunning;
            Loaded += Run;
        }

        // Start immediately, showing progress only when a job outlasts the quiet period.
        // The worker owns retained Mat headers until it stops, including owner shutdown.
        internal static async Task<Exception?> ExecuteAsync(ConoscopeExportSource source, IReadOnlyList<ConoscopeExportJob> jobs, Window? owner)
        {
            ConoscopeExportProgressWindow dialog;
            try { dialog = new(source, jobs) { Owner = owner }; }
            catch { source.Dispose(); throw; }
            bool ownerClosed = false;
            void OwnerClosed(object? sender, EventArgs e)
            {
                ownerClosed = true;
                if (!dialog.finished) dialog.RequestCancellation();
            }
            if (owner != null) owner.Closed += OwnerClosed;
            try
            {
                Task work = dialog.Start();
                await Task.WhenAny(work, Task.Delay(1500));
                bool showedProgress = !work.IsCompleted && !ownerClosed && !dialog.closed;
                if (showedProgress) dialog.ShowDialog();
                await work;
                if (dialog.Error != null && !showedProgress && !ownerClosed)
                    MessageBox.Show(owner, CompositeFormatCache.Format(Properties.Resources.MsgExportFailed, dialog.Error.Message),
                        Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                return dialog.Error;
            }
            finally
            {
                if (owner != null) owner.Closed -= OwnerClosed;
                if (!dialog.closed) dialog.Close();
            }
        }

        private async void Run(object sender, RoutedEventArgs e) => await Start();

        private Task Start() => execution ??= RunAsync();

        private async Task RunAsync()
        {
            if (started) return;
            started = true;
            try
            {
                for (int index = 0; index < jobs.Count; index++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    ConoscopeExportJob job = jobs[index];
                    FileText.Text = job.Path;
                    ExportProgress.Value = 0;
                    ExportProgress.IsIndeterminate = true;
                    ProgressText.Text = CompositeFormatCache.Format(Properties.Resources.ExportFilesCompleted, CompletedFiles, jobs.Count);
                    int currentIndex = index;
                    Progress<ConoscopeExportProgress> progress = new(value =>
                    {
                        if (finished || CompletedFiles != currentIndex) return;
                        ExportProgress.IsIndeterminate = false;
                        ExportProgress.Value = value.TotalSamples == 0 ? 0 : 100.0 * value.CompletedSamples / value.TotalSamples;
                        ProgressText.Text = CompositeFormatCache.Format(Properties.Resources.ExportProgressFormat,
                            currentIndex + 1, jobs.Count, value.CompletedSamples, value.TotalSamples);
                    });
                    ConoscopeExportContext execution = source.Context.ForExecution(cancellation.Token, progress);
                    await Task.Run(() => job.Export(job.Path, execution));
                    CompletedFiles++;
                }
                ExportProgress.Value = 100;
                StatusText.Text = Properties.Resources.ExportCompleted;
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = Properties.Resources.ExportCancelled;
            }
            catch (Exception ex)
            {
                Error = ex;
                StatusText.Text = Properties.Resources.TitleError;
                FileText.Text += Environment.NewLine + ex.Message;
            }
            finally
            {
                finished = true;
                ExportProgress.IsIndeterminate = false;
                source.Dispose();
                if (closed) cancellation.Dispose();
                ProgressText.Text = CompositeFormatCache.Format(Properties.Resources.ExportFilesCompleted, CompletedFiles, jobs.Count);
                CancelButton.Content = Properties.Resources.BtnClose;
                CancelButton.IsEnabled = true;
                // Successful exports need no acknowledgement. Errors and a cancelled
                // batch retain the completed-file count so partial results are clear.
                if (Error == null && CompletedFiles == jobs.Count && IsVisible) Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (finished) Close();
            else RequestCancellation();
        }

        private void RequestCancellation()
        {
            cancellation.Cancel();
            StatusText.Text = Properties.Resources.ExportCancelling;
            CancelButton.IsEnabled = false;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (started && !finished)
            {
                e.Cancel = true;
                RequestCancellation();
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            closed = true;
            if (finished || !started)
            {
                source.Dispose();
                cancellation.Dispose();
            }
            else cancellation.Cancel();
            base.OnClosed(e);
        }
    }
}
