using ColorVision.Algorithms;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

public sealed class BasicAdjustmentWindowOwnershipTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("basic")]
    [InlineData("white-balance")]
    public async Task CompletedUnchangedPreviewIsCommittedWithoutAnotherExecution(string windowKind)
    {
        WriteOwnerIdentity();
        WindowFixture fixture = WpfTestHost.Invoke(() => new WindowFixture(windowKind));
        PreviewExecutionGate? preview = null;
        Task? apply = null;
        try
        {
            preview = await CompleteChangedPreviewAsync(fixture);
            var ready = WpfTestHost.Invoke(() =>
            {
                WriteableBitmap bitmap = Assert.IsType<WriteableBitmap>(fixture.Context.FunctionImage);
                Assert.Same(bitmap, fixture.View.ImageShow.Source);
                Assert.NotSame(fixture.Canonical, bitmap);
                Assert.False(bitmap.IsFrozen);
                Assert.True(preview.Output!.IsDisposed);
                BindingExpression binding = FindNumericTextBox(fixture.Window, fixture.SliderName).GetBindingExpression(TextBox.TextProperty)!;
                Assert.Equal(BindingStatus.Active, binding.Status);
                Assert.False(binding.HasError);
                return (Bitmap: bitmap, Revision: fixture.Context.ImageRevision, Executions: fixture.Provider.ExecutionCount,
                    ParameterRevision: ParameterRevision(fixture.Window), InputRevision: InputRevision(fixture.Window), Claim: fixture.CurrentClaim());
            });
            apply = WpfTestHost.Invoke(() => RaiseApplyAndObserveCompletion(fixture.Window));
            await apply.WaitAsync(TimeSpan.FromSeconds(5));
            var settled = WpfTestHost.Invoke(() =>
            {
                byte[] pixels = new byte[8], originalPixels = new byte[8];
                Assert.IsType<WriteableBitmap>(fixture.Context.ViewBitmapSource).CopyPixels(pixels, 4, 0);
                fixture.Canonical.CopyPixels(originalPixels, 4, 0);
                return new
                {
                    fixture.IsClosed, Executions = fixture.Provider.ExecutionCount, Revision = fixture.Context.ImageRevision,
                    ParameterRevision = ParameterRevision(fixture.Window), InputRevision = InputRevision(fixture.Window),
                    SourceIsReadyBitmap = ReferenceEquals(fixture.Context.ViewBitmapSource, ready.Bitmap),
                    SourceEqualsDisplay = ReferenceEquals(fixture.Context.ViewBitmapSource, fixture.View.ImageShow.Source),
                    FunctionImageNull = fixture.Context.FunctionImage == null, pixels, originalPixels
                };
            });
            output.WriteLine(JsonSerializer.Serialize(new
            {
                windowKind, ready = new { ready.Revision, ready.Executions, ready.ParameterRevision, ready.InputRevision, ready.Claim }, settled
            }));
            Assert.Equal(ready.ParameterRevision, settled.ParameterRevision);
            Assert.Equal(ready.InputRevision, settled.InputRevision);
            Assert.True(settled.IsClosed);
            Assert.Equal(ready.Executions, settled.Executions);
            Assert.True(settled.SourceIsReadyBitmap);
            Assert.True(settled.SourceEqualsDisplay);
            Assert.True(settled.FunctionImageNull);
            Assert.Equal(ready.Revision + 1, settled.Revision);
            Assert.Equal(Enumerable.Repeat((byte)91, 8), settled.pixels);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, settled.originalPixels);
        }
        finally
        {
            preview?.Release.TrySetResult(true);
            try { if (apply != null) await apply.WaitAsync(TimeSpan.FromSeconds(5)); }
            finally { WpfTestHost.Invoke(fixture.Dispose); }
        }
    }

    [Theory]
    [InlineData("basic", 10, 20)]
    [InlineData("white-balance", 1.5, 1.75)]
    public async Task SuccessfulPreviewCannotBeReusedForParametersChangedBeforeItCompleted(string windowKind, double previousValue, double currentValue)
    {
        WriteOwnerIdentity();
        WindowFixture fixture = WpfTestHost.Invoke(() => new WindowFixture(windowKind));
        PreviewExecutionGate? previous = null, current = null;
        Task? apply = null;
        try
        {
            await fixture.Provider.ResultDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var started = WpfTestHost.Invoke(() =>
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                previous = fixture.Provider.GateNextExecution(91);
                fixture.ChangeParameter();
                RunPendingDebounce(fixture.Window);
                return (Claim: Assert.IsType<AlgorithmInvocationClaim>(fixture.CurrentClaim()), ParameterRevision: ParameterRevision(fixture.Window), InputRevision: InputRevision(fixture.Window));
            });
            await previous!.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var changed = WpfTestHost.Invoke(() =>
            {
                if (fixture.Window is BasicAdjustmentWindow)
                    Assert.True(Assert.IsType<CheckBox>(fixture.Window.FindName("PreviewCheckBox")).IsChecked);
                fixture.Brightness.Value = currentValue;
                // Hold Q at the debounce boundary without cancelling the still-current P invocation.
                string key = (string)fixture.Window.GetType().GetField("_debounceKey", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Window)!;
                DebounceTimer.Cancel(key);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Assert.Equal(started.Claim, fixture.CurrentClaim());
                Assert.Equal(2, fixture.Provider.ExecutionCount);
                return (ParameterRevision: ParameterRevision(fixture.Window), InputRevision: InputRevision(fixture.Window));
            });
            previous.Release.TrySetResult(true);
            await previous.ResultDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var ready = WpfTestHost.Invoke(() =>
            {
                ImageAlgorithmPreviewSession session = (ImageAlgorithmPreviewSession)fixture.Window.GetType().GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Window)!;
                Assert.True(session.IsCurrent(started.Claim.InvocationId));
                Assert.Equal(started.Claim, fixture.CurrentClaim());
                Assert.Equal(previousValue, previous.ParameterValue);
                Assert.True(previous.Output!.IsDisposed);
                WriteableBitmap bitmap = Assert.IsType<WriteableBitmap>(fixture.Context.FunctionImage);
                byte[] pixels = new byte[8];
                bitmap.CopyPixels(pixels, 4, 0);
                Assert.Equal(Enumerable.Repeat((byte)91, 8), pixels);
                return (Bitmap: bitmap, Revision: fixture.Context.ImageRevision, Executions: fixture.Provider.ExecutionCount,
                    ParameterRevision: ParameterRevision(fixture.Window), InputRevision: InputRevision(fixture.Window));
            });
            WpfTestHost.Invoke(() =>
            {
                current = fixture.Provider.GateNextExecution(97);
                current.Release.TrySetResult(true);
                apply = RaiseApplyAndObserveCompletion(fixture.Window);
            });
            await apply!.WaitAsync(TimeSpan.FromSeconds(5));
            var settled = WpfTestHost.Invoke(() =>
            {
                byte[] pixels = new byte[8];
                Assert.IsType<WriteableBitmap>(fixture.Context.ViewBitmapSource).CopyPixels(pixels, 4, 0);
                return new
                {
                    fixture.IsClosed, Executions = fixture.Provider.ExecutionCount, Revision = fixture.Context.ImageRevision,
                    ParameterValue = current!.ParameterValue, SourceIsPreviousBitmap = ReferenceEquals(ready.Bitmap, fixture.Context.ViewBitmapSource), pixels
                };
            });
            output.WriteLine(JsonSerializer.Serialize(new
            {
                windowKind, started = new { started.Claim, started.ParameterRevision, started.InputRevision },
                changed = new { changed.ParameterRevision, changed.InputRevision },
                ready = new { ready.Revision, ready.Executions, ready.ParameterRevision, ready.InputRevision }, settled
            }));
            Assert.True(changed.ParameterRevision > started.ParameterRevision);
            Assert.True(changed.InputRevision > started.InputRevision);
            Assert.Equal(changed.ParameterRevision, ready.ParameterRevision);
            Assert.Equal(changed.InputRevision, ready.InputRevision);
            Assert.Equal(2, ready.Executions);
            Assert.True(settled.IsClosed);
            Assert.Equal(ready.Executions + 1, settled.Executions);
            Assert.Equal(currentValue, settled.ParameterValue);
            Assert.False(settled.SourceIsPreviousBitmap);
            Assert.Equal(ready.Revision + 1, settled.Revision);
            Assert.Equal(Enumerable.Repeat((byte)97, 8), settled.pixels);
        }
        finally
        {
            previous?.Release.TrySetResult(true);
            current?.Release.TrySetResult(true);
            try { if (apply != null) await apply.WaitAsync(TimeSpan.FromSeconds(5)); }
            finally { WpfTestHost.Invoke(fixture.Dispose); }
        }
    }

    [Theory]
    [InlineData("basic", "analysis")]
    [InlineData("basic", "new-preview")]
    [InlineData("white-balance", "analysis")]
    [InlineData("white-balance", "new-preview")]
    public async Task ReadyPreviewCommitDoesNotReclaimAReplacementOwner(string windowKind, string takeover)
    {
        WriteOwnerIdentity();
        WindowFixture fixture = WpfTestHost.Invoke(() => new WindowFixture(windowKind));
        ImageAlgorithmPreviewSession? replacement = null;
        CancellationTokenSource? analysis = null;
        Task? apply = null;
        AlgorithmInvocationClaim? replacementClaim = null;
        int commitCallbacks = 0;
        try
        {
            await CompleteChangedPreviewAsync(fixture);
            var ready = WpfTestHost.Invoke(() =>
            {
                long revision = fixture.Context.ImageRevision;
                int executions = fixture.Provider.ExecutionCount;
                fixture.View.AlgorithmPreviewCommitHook = _ =>
                {
                    if (commitCallbacks++ != 0) return;
                    if (takeover == "analysis") analysis = fixture.BeginAnalysis();
                    else replacement = ImageAlgorithmPreviewSession.StartForCatalogPreview(fixture.Context);
                    replacementClaim = fixture.CurrentClaim();
                };
                return (Revision: revision, Executions: executions);
            });
            apply = WpfTestHost.Invoke(() => RaiseApplyAndObserveCompletion(fixture.Window));
            await apply.WaitAsync(TimeSpan.FromSeconds(5));
            var settled = WpfTestHost.Invoke(() => new
            {
                fixture.IsClosed, Executions = fixture.Provider.ExecutionCount, Revision = fixture.Context.ImageRevision,
                CurrentClaim = fixture.CurrentClaim(), replacementClaim, commitCallbacks,
                ReplacementPreviewOwnsHost = replacement?.OwnsHostPreview,
                AnalysisCancelled = analysis?.IsCancellationRequested,
                SourceIsOriginal = ReferenceEquals(fixture.Canonical, fixture.Context.ViewBitmapSource),
                DisplayIsOriginal = ReferenceEquals(fixture.Canonical, fixture.View.ImageShow.Source),
                FunctionImageNull = fixture.Context.FunctionImage == null
            });
            output.WriteLine(JsonSerializer.Serialize(new { windowKind, takeover, ready = new { ready.Revision, ready.Executions }, settled }));
            Assert.Equal(ready.Executions, settled.Executions);
            Assert.Equal(1, settled.commitCallbacks);
            Assert.False(settled.IsClosed);
            Assert.NotNull(replacementClaim);
            Assert.Equal(replacementClaim, settled.CurrentClaim);
            Assert.Equal(ready.Revision, settled.Revision);
            Assert.True(settled.SourceIsOriginal);
            Assert.True(settled.DisplayIsOriginal);
            Assert.True(settled.FunctionImageNull);
            if (analysis != null) Assert.False(settled.AnalysisCancelled);
            if (replacement != null) Assert.True(settled.ReplacementPreviewOwnsHost);
        }
        finally
        {
            try { if (apply != null) await apply.WaitAsync(TimeSpan.FromSeconds(5)); }
            finally
            {
                WpfTestHost.Invoke(() =>
                {
                    fixture.View.AlgorithmPreviewCommitHook = null;
                    replacement?.Dispose();
                    fixture.Dispose();
                });
                analysis?.Dispose();
            }
        }
    }

    private void WriteOwnerIdentity()
    {
        string path = typeof(BasicAdjustmentWindow).Assembly.Location;
        output.WriteLine($"ImageEditor: {path}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}");
    }

    private static long ParameterRevision(Window window)
        => (long)window.GetType().GetField("_parameterRevision", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;

    private static long InputRevision(Window window)
        => ((AlgorithmParameterInputs)window.GetType().GetField("_parameterInputs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Revision;

    private static async Task<PreviewExecutionGate> CompleteChangedPreviewAsync(WindowFixture fixture)
    {
        await fixture.Provider.ResultDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        PreviewExecutionGate preview = WpfTestHost.Invoke(() =>
        {
            // Let initial target bindings settle before starting the preview whose state Apply will reuse.
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            PreviewExecutionGate run = fixture.Provider.GateNextExecution(91);
            run.Release.TrySetResult(true);
            fixture.WaitForSourcePublication(() =>
            {
                fixture.ChangeParameter();
                RunPendingDebounce(fixture.Window);
            });
            return run;
        });
        // Window-owned result disposal occurs after its await continuation has consumed success.
        await preview.ResultDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        return preview;
    }

    [Theory]
    [InlineData("30", 30)]
    [InlineData("200", 100)]
    public async Task DefaultConfirmationCommitsTheDirtyNumericBindingOnce(string draft, double expected)
    {
        WindowFixture fixture = WpfTestHost.Invoke(() => new WindowFixture());
        PreviewExecutionGate? execution = null;
        Task? apply = null;
        try
        {
            long revision = WpfTestHost.Invoke(() => fixture.Context.ImageRevision);
            WpfTestHost.Invoke(() =>
            {
                ShowForDefaultConfirmation(fixture.Window);
                Assert.IsType<CheckBox>(fixture.Window.FindName("PreviewCheckBox")).IsChecked = false;
                TextBox input = FindNumericTextBox(fixture.Window, "BrightnessSlider");
                BindingExpression binding = input.GetBindingExpression(TextBox.TextProperty)!;
                input.SetCurrentValue(TextBox.TextProperty, draft);
                Assert.Same(binding, input.GetBindingExpression(TextBox.TextProperty));
                Assert.Equal(0, fixture.Brightness.Value);
                execution = fixture.Provider.GateNextExecution(97);
                execution.Release.TrySetResult(true);
                apply = RaiseApplyAndObserveCompletion(fixture.Window, defaultAccessKey: true);
            });
            await apply!.WaitAsync(TimeSpan.FromSeconds(5));

            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(expected, execution!.ParameterValue);
                Assert.True(fixture.IsClosed);
                Assert.Equal(2, fixture.Provider.ExecutionCount);
                Assert.Equal(revision + 1, fixture.Context.ImageRevision);
                Assert.Equal(expected.ToString("F0", System.Globalization.CultureInfo.InvariantCulture), FindNumericTextBox(fixture.Window, "BrightnessSlider").Text);
                Assert.Null(fixture.Context.FunctionImage);
                Assert.NotNull(execution.Output);
                Assert.True(execution.Output.IsDisposed);
            });
        }
        finally
        {
            execution?.Release.TrySetResult(true);
            try { if (apply != null) await apply.WaitAsync(TimeSpan.FromSeconds(5)); }
            finally { WpfTestHost.Invoke(fixture.Dispose); }
        }
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("NaN")]
    public async Task FailedNumericBindingCannotApplyTheOldValueAndCanBeCorrected(string draft)
    {
        WindowFixture fixture = WpfTestHost.Invoke(() => new WindowFixture());
        PreviewExecutionGate? corrected = null;
        Task? invalidApply = null, repeatedApply = null, correctedApply = null;
        ValidationError? firstError = null;
        object? originalToolTip = null, feedbackToolTip = null;
        try
        {
            long revision = WpfTestHost.Invoke(() => fixture.Context.ImageRevision);
            WpfTestHost.Invoke(() =>
            {
                Assert.IsType<CheckBox>(fixture.Window.FindName("PreviewCheckBox")).IsChecked = false;
                TextBox input = FindNumericTextBox(fixture.Window, "BrightnessSlider");
                BindingExpression binding = input.GetBindingExpression(TextBox.TextProperty)!;
                originalToolTip = input.ToolTip;
                input.SetCurrentValue(TextBox.TextProperty, draft);
                binding.UpdateSource(); // Reproduce the binding state after a failed focus-loss update.
                Assert.Equal(BindingStatus.UpdateSourceError, binding.Status);
                if (draft == "NaN")
                {
                    Assert.False(binding.HasError);
                    Assert.False(Validation.GetHasError(input));
                }
                Assert.Equal(0, fixture.Brightness.Value);
                invalidApply = RaiseApplyAndObserveCompletion(fixture.Window);
            });
            await invalidApply!.WaitAsync(TimeSpan.FromSeconds(5));

            WpfTestHost.Invoke(() =>
            {
                Assert.False(fixture.IsClosed);
                Assert.Equal(1, fixture.Provider.ExecutionCount);
                Assert.Equal(revision, fixture.Context.ImageRevision);
                Assert.Same(fixture.Canonical, fixture.Context.ViewBitmapSource);
                Assert.Same(fixture.Canonical, fixture.View.ImageShow.Source);
                Assert.Null(fixture.Context.FunctionImage);
                TextBox input = FindNumericTextBox(fixture.Window, "BrightnessSlider");
                Assert.Equal(draft, input.Text);
                BindingExpression binding = input.GetBindingExpression(TextBox.TextProperty)!;
                Assert.True(Validation.GetHasError(input));
                firstError = Assert.IsType<ValidationError>(binding.ValidationError);
                Assert.False(string.IsNullOrWhiteSpace(firstError.ErrorContent?.ToString()));
                if (draft == "bad") Assert.Equal("ConversionValidationRule", firstError.RuleInError.GetType().Name);
                feedbackToolTip = input.ToolTip;
                Assert.False(string.IsNullOrWhiteSpace(ToolTipText(feedbackToolTip)));
                repeatedApply = RaiseApplyAndObserveCompletion(fixture.Window);
            });
            await repeatedApply!.WaitAsync(TimeSpan.FromSeconds(5));
            WpfTestHost.Invoke(() =>
            {
                Assert.False(fixture.IsClosed);
                Assert.Equal(1, fixture.Provider.ExecutionCount);
                Assert.Equal(revision, fixture.Context.ImageRevision);
                TextBox input = FindNumericTextBox(fixture.Window, "BrightnessSlider");
                BindingExpression binding = input.GetBindingExpression(TextBox.TextProperty)!;
                Assert.Equal(draft, input.Text);
                Assert.True(Validation.GetHasError(input));
                Assert.NotNull(binding.ValidationError);
                if (draft == "NaN") Assert.Same(firstError, binding.ValidationError);
                else Assert.Equal("ConversionValidationRule", binding.ValidationError.RuleInError.GetType().Name);
                Assert.Same(feedbackToolTip, input.ToolTip);
                Assert.False(string.IsNullOrWhiteSpace(ToolTipText(input.ToolTip)));
                input.SetCurrentValue(TextBox.TextProperty, "30");
                Assert.Equal(0, fixture.Brightness.Value);
                corrected = fixture.Provider.GateNextExecution(97);
                corrected.Release.TrySetResult(true);
                correctedApply = RaiseApplyAndObserveCompletion(fixture.Window);
            });
            await correctedApply!.WaitAsync(TimeSpan.FromSeconds(5));
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(30, corrected!.ParameterValue);
                Assert.True(fixture.IsClosed);
                Assert.Equal(2, fixture.Provider.ExecutionCount);
                Assert.Equal(revision + 1, fixture.Context.ImageRevision);
                Assert.Null(fixture.Context.FunctionImage);
                Assert.True(corrected.Output!.IsDisposed);
                TextBox input = FindNumericTextBox(fixture.Window, "BrightnessSlider");
                Assert.False(Validation.GetHasError(input));
                Assert.Null(input.GetBindingExpression(TextBox.TextProperty)!.ValidationError);
                Assert.Equal(originalToolTip, input.ToolTip);
            });
        }
        finally
        {
            corrected?.Release.TrySetResult(true);
            try
            {
                if (invalidApply != null) await invalidApply.WaitAsync(TimeSpan.FromSeconds(5));
                if (repeatedApply != null) await repeatedApply.WaitAsync(TimeSpan.FromSeconds(5));
                if (correctedApply != null) await correctedApply.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally { WpfTestHost.Invoke(fixture.Dispose); }
        }
    }

    [Fact]
    public async Task ClosingAWindowDoesNotClearReplacementValidationOrToolTip()
    {
        WindowFixture fixture = WpfTestHost.Invoke(() => new WindowFixture());
        Task? invalidApply = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                Assert.IsType<CheckBox>(fixture.Window.FindName("PreviewCheckBox")).IsChecked = false;
                TextBox input = FindNumericTextBox(fixture.Window, "BrightnessSlider");
                input.SetCurrentValue(TextBox.TextProperty, "NaN");
                invalidApply = RaiseApplyAndObserveCompletion(fixture.Window);
            });
            await invalidApply!.WaitAsync(TimeSpan.FromSeconds(5));
            WpfTestHost.Invoke(() =>
            {
                TextBox input = FindNumericTextBox(fixture.Window, "BrightnessSlider");
                BindingExpression binding = input.GetBindingExpression(TextBox.TextProperty)!;
                ValidationError owned = Assert.IsType<ValidationError>(binding.ValidationError);
                Assert.IsType<ToolTip>(input.ToolTip);
                ValidationError successor = new(new ExceptionValidationRule(), binding, "Successor validation owner", null);
                ToolTip successorToolTip = new() { Content = "Successor tooltip owner" };
                Assert.NotSame(owned, successor);
                Validation.MarkInvalid(binding, successor);
                input.SetCurrentValue(FrameworkElement.ToolTipProperty, successorToolTip);

                fixture.Window.Close(); // Never shown: no focus-loss or HWND teardown can perform the cleanup for the helper.

                Assert.True(fixture.IsClosed);
                Assert.Same(binding, input.GetBindingExpression(TextBox.TextProperty));
                Assert.Same(successor, binding.ValidationError);
                Assert.True(Validation.GetHasError(input));
                Assert.Same(successorToolTip, input.ToolTip);
            });
        }
        finally
        {
            try { if (invalidApply != null) await invalidApply.WaitAsync(TimeSpan.FromSeconds(5)); }
            finally { WpfTestHost.Invoke(fixture.Dispose); }
        }
    }

    [Fact]
    public async Task EditingOnlyTheRawNumericDraftWhileApplyingRequiresANewExplicitApply()
    {
        WindowFixture fixture = WpfTestHost.Invoke(() => new WindowFixture());
        PreviewExecutionGate? previous = null, current = null;
        Task? previousApply = null, currentApply = null;
        try
        {
            long revision = WpfTestHost.Invoke(() => fixture.Context.ImageRevision);
            WpfTestHost.Invoke(() =>
            {
                Assert.IsType<CheckBox>(fixture.Window.FindName("PreviewCheckBox")).IsChecked = false;
                fixture.Brightness.Value = 10;
                previous = fixture.Provider.GateNextExecution(87);
                previousApply = RaiseApplyAndObserveCompletion(fixture.Window);
            });
            await previous!.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            WpfTestHost.Invoke(() =>
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                TextBox input = FindNumericTextBox(fixture.Window, "BrightnessSlider");
                BindingExpression binding = input.GetBindingExpression(TextBox.TextProperty)!;
                Assert.Equal("10", input.Text);
                input.SetCurrentValue(TextBox.TextProperty, "20");
                Assert.Same(binding, input.GetBindingExpression(TextBox.TextProperty));
                Assert.Equal(10, fixture.Brightness.Value);
                Assert.Equal(2, fixture.Provider.ExecutionCount);
            });
            previous.Release.TrySetResult(true);
            await previousApply!.WaitAsync(TimeSpan.FromSeconds(5));
            WpfTestHost.Invoke(() =>
            {
                Assert.False(fixture.IsClosed);
                Assert.Same(fixture.Canonical, fixture.Context.ViewBitmapSource);
                Assert.Same(fixture.Canonical, fixture.View.ImageShow.Source);
                Assert.Null(fixture.Context.FunctionImage);
                Assert.Equal(revision, fixture.Context.ImageRevision);
                Assert.True(previous.Input!.IsDisposed);
                Assert.True(previous.Output!.IsDisposed);
                current = fixture.Provider.GateNextExecution(97);
                current.Release.TrySetResult(true);
                currentApply = RaiseApplyAndObserveCompletion(fixture.Window);
            });
            await currentApply!.WaitAsync(TimeSpan.FromSeconds(5));
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(20, current!.ParameterValue);
                Assert.True(fixture.IsClosed);
                Assert.Equal(3, fixture.Provider.ExecutionCount);
                Assert.Equal(revision + 1, fixture.Context.ImageRevision);
                Assert.Null(fixture.Context.FunctionImage);
                Assert.True(current.Output!.IsDisposed);
            });
        }
        finally
        {
            previous?.Release.TrySetResult(true);
            current?.Release.TrySetResult(true);
            try
            {
                if (previousApply != null) await previousApply.WaitAsync(TimeSpan.FromSeconds(5));
                if (currentApply != null) await currentApply.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally { WpfTestHost.Invoke(fixture.Dispose); }
        }
    }

    [Fact]
    public async Task ChangingParametersWhileApplyingWithPreviewOffRequiresANewExplicitApply()
    {
        WindowFixture fixture = WpfTestHost.Invoke(() => new WindowFixture());
        PreviewExecutionGate? previous = null, current = null;
        Task? previousApply = null, currentApply = null;
        try
        {
            long revision = WpfTestHost.Invoke(() => fixture.Context.ImageRevision);
            WpfTestHost.Invoke(() =>
            {
                Assert.IsType<CheckBox>(fixture.Window.FindName("PreviewCheckBox")).IsChecked = false;
                fixture.Brightness.Value = 10;
                previous = fixture.Provider.GateNextExecution(outputPixel: 87);
                previousApply = RaiseApplyAndObserveCompletion(fixture.Window);
            });
            await previous!.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            WpfTestHost.Invoke(() =>
            {
                Assert.True(fixture.Brightness.IsEnabled);
                fixture.Brightness.Value = 20;
                Assert.Equal(2, fixture.Provider.ExecutionCount); // Neutral constructor and the explicit P request; Q is not auto-previewed.
            });
            previous.Release.TrySetResult(true);
            await previousApply!.WaitAsync(TimeSpan.FromSeconds(5));

            WpfTestHost.Invoke(() =>
            {
                Assert.False(fixture.IsClosed);
                Assert.Equal(10, previous.ParameterValue);
                Assert.Same(fixture.Canonical, fixture.Context.ViewBitmapSource);
                Assert.Same(fixture.Canonical, fixture.View.ImageShow.Source);
                Assert.Null(fixture.Context.FunctionImage);
                Assert.Equal(revision, fixture.Context.ImageRevision);
                Assert.NotNull(previous.Input);
                Assert.True(previous.Input.IsDisposed);
                Assert.NotNull(previous.Output);
                Assert.True(previous.Output.IsDisposed);
                current = fixture.Provider.GateNextExecution(outputPixel: 97);
                current.Release.TrySetResult(true);
                currentApply = RaiseApplyAndObserveCompletion(fixture.Window);
            });
            await currentApply!.WaitAsync(TimeSpan.FromSeconds(5));

            WpfTestHost.Invoke(() =>
            {
                Assert.True(fixture.IsClosed);
                Assert.Equal(20, current!.ParameterValue);
                Assert.Equal(3, fixture.Provider.ExecutionCount);
                Assert.Equal(revision + 1, fixture.Context.ImageRevision);
                Assert.NotSame(fixture.Canonical, fixture.Context.ViewBitmapSource);
                Assert.Same(fixture.Context.ViewBitmapSource, fixture.View.ImageShow.Source);
                Assert.Null(fixture.Context.FunctionImage);
                byte[] pixel = new byte[1];
                Assert.IsType<WriteableBitmap>(fixture.Context.ViewBitmapSource).CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 1, 0);
                Assert.Equal((byte)97, pixel[0]);
                Assert.NotNull(current.Output);
                Assert.True(current.Output.IsDisposed);
            });
        }
        finally
        {
            previous?.Release.TrySetResult(true);
            current?.Release.TrySetResult(true);
            try
            {
                if (previousApply != null) await previousApply.WaitAsync(TimeSpan.FromSeconds(5));
                if (currentApply != null) await currentApply.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                WpfTestHost.Invoke(fixture.Dispose);
            }
        }
    }

    [Fact]
    public async Task ClosingWindowBeforeItsFirstResultDisposesTheLateImageWithoutPublishing()
    {
        TaskCompletionSource<bool> releaseProvider = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WindowFixture fixture = WpfTestHost.Invoke(() => new WindowFixture(releaseProvider: releaseProvider.Task));
        try
        {
            await fixture.Provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            long revision = WpfTestHost.Invoke(() => fixture.Context.ImageRevision);
            WpfTestHost.Invoke(() =>
            {
                Assert.NotNull(fixture.CurrentClaim());
                Assert.Same(fixture.Canonical, fixture.View.ImageShow.Source);
                Assert.Null(fixture.Context.FunctionImage);
                fixture.Window.Close();
                Assert.Null(fixture.CurrentClaim());
            });
            releaseProvider.TrySetResult(true);
            await fixture.Provider.ResultDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));

            WpfTestHost.Invoke(() =>
            {
                Assert.NotNull(fixture.Provider.ProducedImage);
                Assert.True(fixture.Provider.ProducedImage.IsDisposed);
                Assert.Same(fixture.Canonical, fixture.Context.ViewBitmapSource);
                Assert.Same(fixture.Canonical, fixture.View.ImageShow.Source);
                Assert.Null(fixture.Context.FunctionImage);
                Assert.Null(fixture.CurrentClaim());
                Assert.Equal(revision, fixture.Context.ImageRevision);
            });
        }
        finally
        {
            releaseProvider.TrySetResult(true);
            try
            {
                await fixture.Provider.ResultDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                WpfTestHost.Invoke(fixture.Dispose);
            }
        }
    }

    [Theory]
    [InlineData("white-balance")]
    [InlineData("threshold")]
    [InlineData("edge-detection")]
    [InlineData("gaussian-blur")]
    [InlineData("median-blur")]
    [InlineData("morphology")]
    [InlineData("denoise")]
    public void OtherPreviewWindowsCannotReclaimAnalysisFromQueuedParameterChanges(string windowKind)
    {
        WpfTestHost.Invoke(() =>
        {
            using WindowFixture fixture = new(windowKind);
            fixture.ChangeParameter();
            using CancellationTokenSource analysis = fixture.BeginAnalysis();
            AlgorithmInvocationClaim? expected = fixture.CurrentClaim();
            ImageSource displayed = fixture.View.ImageShow.Source;
            int executionCount = fixture.Provider.ExecutionCount;

            RunPendingDebounce(fixture.Window);

            Assert.Equal(expected, fixture.CurrentClaim());
            Assert.False(analysis.IsCancellationRequested);
            Assert.Equal(executionCount, fixture.Provider.ExecutionCount);
            Assert.Same(displayed, fixture.View.ImageShow.Source);
        });
    }

    [Theory]
    [InlineData("analysis")]
    [InlineData("new-preview")]
    [InlineData("display-selection")]
    public void QueuedSliderChangeCannotReclaimAfterAnotherOperationTakesOver(string takeover)
    {
        WpfTestHost.Invoke(() =>
        {
            using WindowFixture fixture = new();
            ImageAlgorithmPreviewSession? newer = null;
            CancellationTokenSource? analysis = null;
            try
            {
                fixture.Brightness.Value = 10; // Real XAML ValueChanged handler, not a private-handler call.
                switch (takeover)
                {
                    case "analysis":
                        analysis = fixture.BeginAnalysis();
                        break;
                    case "new-preview":
                        newer = ImageAlgorithmPreviewSession.Start(fixture.Context);
                        break;
                    case "display-selection":
                        fixture.Context.SupersedePreviewForDisplaySelection();
                        break;
                }
                AlgorithmInvocationClaim? expected = fixture.CurrentClaim();
                ImageSource displayed = fixture.View.ImageShow.Source;
                int executionCount = fixture.Provider.ExecutionCount;

                RunPendingDebounce(fixture.Window);

                Assert.Equal(expected, fixture.CurrentClaim());
                Assert.Equal(executionCount, fixture.Provider.ExecutionCount);
                Assert.Same(displayed, fixture.View.ImageShow.Source);
                if (analysis != null) Assert.False(analysis.IsCancellationRequested);
                if (newer != null) Assert.True(newer.OwnsHostPreview);
            }
            finally
            {
                newer?.Dispose();
                fixture.ReleaseAnalysis();
                analysis?.Dispose();
            }
        });
    }

    [Fact]
    public void QueuedSliderChangeCannotReclaimAfterAnOwnerlessAnalysisRoundTrip()
    {
        WpfTestHost.Invoke(() =>
        {
            using WindowFixture fixture = new();
            using (CancellationTokenSource previous = fixture.BeginAnalysis()) fixture.ReleaseAnalysis();
            Assert.Null(fixture.CurrentClaim());
            fixture.Brightness.Value = 10;
            using (CancellationTokenSource newer = fixture.BeginAnalysis()) fixture.ReleaseAnalysis();
            Assert.Null(fixture.CurrentClaim());
            int executionCount = fixture.Provider.ExecutionCount;

            RunPendingDebounce(fixture.Window);

            Assert.Null(fixture.CurrentClaim());
            Assert.Equal(executionCount, fixture.Provider.ExecutionCount);
            Assert.Same(fixture.Canonical, fixture.View.ImageShow.Source);
        });
    }

    [Theory]
    [InlineData("basic", false)]
    [InlineData("basic", true)]
    [InlineData("white-balance", false)]
    public void NewSliderChangeAfterTakeoverCanStartAnExplicitNewPreview(string windowKind, bool releaseAnalysis)
    {
        WpfTestHost.Invoke(() =>
        {
            using WindowFixture fixture = new(windowKind);
            using CancellationTokenSource analysis = fixture.BeginAnalysis();
            if (releaseAnalysis) fixture.ReleaseAnalysis();
            AlgorithmInvocationClaim? previous = fixture.CurrentClaim();
            int executionCount = fixture.Provider.ExecutionCount;

            fixture.WaitForSourcePublication(() =>
            {
                fixture.ChangeParameter();
                RunPendingDebounce(fixture.Window);
            });

            AlgorithmInvocationClaim? current = fixture.CurrentClaim();
            Assert.NotNull(current);
            Assert.NotEqual(previous, current);
            Assert.Equal(executionCount + 1, fixture.Provider.ExecutionCount);
            Assert.True(analysis.IsCancellationRequested);
            Assert.NotSame(fixture.Canonical, fixture.View.ImageShow.Source);
            Assert.Same(fixture.Context.FunctionImage, fixture.View.ImageShow.Source);
        });
    }

    private static void ShowForDefaultConfirmation(Window window)
    {
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = SystemParameters.VirtualScreenLeft - 3000;
        window.Top = SystemParameters.VirtualScreenTop - 2000;
        window.Show();
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        Assert.True(window.IsLoaded);
        Assert.NotNull(PresentationSource.FromVisual(window));
    }

    private static TextBox FindNumericTextBox(DependencyObject root, string sliderName)
        => FindNumericTextBoxCore(root, sliderName) ?? throw new InvalidOperationException($"No numeric binding for {sliderName}.");

    private static string ToolTipText(object? value) => value switch
    {
        ToolTip { Content: TextBlock text } => text.Text,
        ToolTip tip => tip.Content?.ToString() ?? string.Empty,
        TextBlock text => text.Text,
        _ => value?.ToString() ?? string.Empty,
    };

    private static TextBox? FindNumericTextBoxCore(DependencyObject root, string sliderName)
    {
        if (root is TextBox text && text.GetBindingExpression(TextBox.TextProperty)?.ParentBinding is Binding binding
            && binding.ElementName == sliderName && binding.Path?.Path == nameof(RangeBase.Value)) return text;
        foreach (object child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject element && FindNumericTextBoxCore(element, sliderName) is TextBox found) return found;
        return null;
    }

    private static Task RaiseApplyAndObserveCompletion(Window window, bool defaultAccessKey = false)
    {
        // White balance's XAML identifies Apply by localized content rather than IsDefault.
        Button apply = FindApplyButton(window, window is WhiteBalanceWindow) ?? throw new InvalidOperationException("The actual Apply button was not found.");
        Assert.True(apply.IsEnabled);
        SynchronizationContext? previous = SynchronizationContext.Current;
        AsyncVoidCompletionContext observer = new(previous ?? new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        SynchronizationContext.SetSynchronizationContext(observer);
        try
        {
            if (defaultAccessKey) AccessKeyManager.ProcessKey(PresentationSource.FromVisual(window), "\r", false);
            else apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
            Assert.True(observer.ObservedOperation, "The Apply routed click did not enter its async-void handler.");
            return observer.Completed.Task;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static Button? FindApplyButton(DependencyObject root, bool matchApplyContent)
    {
        if (root is Button button && (button.IsDefault || matchApplyContent
            && Equals(button.Content, ColorVision.ImageEditor.Properties.Resources.Algorithm_Apply))) return button;
        foreach (object child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject element && FindApplyButton(element, matchApplyContent) is Button found) return found;
        return null;
    }

    private static void RunPendingDebounce(Window window)
    {
        // Inspect only our debounce adapter to subscribe to its real public Timer.Elapsed event.
        // Restarting that one timer establishes a deterministic gate while the UI transaction
        // still prevents its queued callback from running. No BCL-private event fields or
        // private window handlers are invoked, and no elapsed-time acceptance limit is used.
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        string key = (string)window.GetType().GetField("_debounceKey", instance)!.GetValue(window)!;
        object scheduler = typeof(DebounceTimer).GetField("Scheduler", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        IDictionary entries = (IDictionary)scheduler.GetType().GetField("_entries", instance)!.GetValue(scheduler)!;
        object? entry = entries[key];
        Assert.NotNull(entry);
        object adapter = entry.GetType().GetProperty("Timer", instance | BindingFlags.Public)!.GetValue(entry)!;
        System.Timers.Timer timer = (System.Timers.Timer)adapter.GetType().GetField("_timer", instance)!.GetValue(adapter)!;
        using ManualResetEventSlim callbackQueued = new();
        System.Timers.ElapsedEventHandler elapsed = (_, _) => callbackQueued.Set();
        timer.Stop();
        timer.Elapsed += elapsed;
        try
        {
            timer.Start();
            Assert.True(callbackQueued.Wait(TimeSpan.FromSeconds(5)), "Watchdog: the real debounce timer did not queue its callback.");
        }
        finally
        {
            timer.Stop();
            timer.Elapsed -= elapsed;
        }
        DispatcherFrame frame = new();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class WindowFixture : IDisposable
    {
        private Guid _analysisInvocation;
        private readonly WindowCase _windowCase;
        private bool _windowClosed;

        public WindowFixture(string windowKind = "basic", Task? releaseProvider = null)
        {
            _windowCase = WindowCase.For(windowKind);
            ResourceDictionary resources = Application.Current.Resources;
            resources["TextBox.Small"] = new Style(typeof(TextBox));
            resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            resources["ToolBarImage"] = new Style(typeof(Image));
            resources["BaseStyle"] = new Style(typeof(Control));
            resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
            resources["GlobalBackground"] = Brushes.White;
            AlgorithmCatalog catalog = new();
            catalog.Register(new AlgorithmDescriptor(_windowCase.AlgorithmId, new AlgorithmVersion(1, 0, 0),
                windowKind, "test", "Window ownership boundary", _windowCase.Parameters.GetType(),
                new AlgorithmParameterSchema(_windowCase.Parameters.SchemaVersion, Array.Empty<AlgorithmParameterField>(), AlgorithmJson.ToElement(_windowCase.Parameters)),
                new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 },
                AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                OutputFormats: new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 }));
            Provider = new ImmediatePreviewProvider(_windowCase.AlgorithmId, releaseProvider);
            AlgorithmRuntime runtime = new(catalog, [Provider], new ImmediateScheduler());
            View = new ImageView(runtime);
            Canonical = new WriteableBitmap(4, 2, 96, 96, PixelFormats.Gray8, null);
            Canonical.WritePixels(new Int32Rect(0, 0, 4, 2), new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 4, 0);
            View.SetImageSource(Canonical, enableEditorImageServices: false, configureDefaultLayerController: false);
            Context = View.EditorContext.ProcessingContext;
            Window? window = null;
            if (releaseProvider == null) WaitForSourcePublication(() => window = _windowCase.CreateWindow(Context));
            else window = _windowCase.CreateWindow(Context);
            Window = Assert.IsAssignableFrom<Window>(window);
            Window.Closed += (_, _) => _windowClosed = true;
            Brightness = Assert.IsAssignableFrom<RangeBase>(Window.FindName(_windowCase.SliderName));
            Assert.Equal(1, Provider.ExecutionCount);
        }

        public ImmediatePreviewProvider Provider { get; }
        public ImageView View { get; }
        public ImageProcessingContext Context { get; }
        public WriteableBitmap Canonical { get; }
        public Window Window { get; }
        public RangeBase Brightness { get; }
        public string SliderName => _windowCase.SliderName;
        public bool IsClosed => _windowClosed;

        public void ChangeParameter()
        {
            Assert.NotEqual(_windowCase.ChangedValue, Brightness.Value);
            Brightness.Value = _windowCase.ChangedValue;
        }

        public void WaitForSourcePublication(Action start)
        {
            bool publishing = false, published = false;
            DispatcherFrame frame = new();
            Action<AlgorithmInvocationClaim>? previous = View.AlgorithmPreviewPublicationHook;
            DependencyPropertyDescriptor sourceProperty = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(DrawCanvas))!;
            EventHandler sourceChanged = (_, _) =>
            {
                if (!publishing) return;
                published = true;
                frame.Continue = false;
            };
            DispatcherTimer watchdog = new(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(5) };
            EventHandler watchdogElapsed = (_, _) => frame.Continue = false;
            View.AlgorithmPreviewPublicationHook = claim =>
            {
                previous?.Invoke(claim);
                publishing = true;
            };
            sourceProperty.AddValueChanged(View.ImageShow, sourceChanged);
            watchdog.Tick += watchdogElapsed;
            try
            {
                start();
                if (!published)
                {
                    watchdog.Start();
                    Dispatcher.PushFrame(frame);
                }
                Assert.True(published, "Watchdog: the requested preview did not publish its bitmap Source.");
            }
            finally
            {
                watchdog.Stop();
                watchdog.Tick -= watchdogElapsed;
                sourceProperty.RemoveValueChanged(View.ImageShow, sourceChanged);
                View.AlgorithmPreviewPublicationHook = previous;
            }
        }

        public AlgorithmInvocationClaim? CurrentClaim()
            => Context.AlgorithmRuntime.InvocationCoordinator.TryGetCurrent(new AlgorithmInvocationScope(Context.DocumentInstanceId, Context.ImageRevision), out AlgorithmInvocationClaim claim) ? claim : null;

        public CancellationTokenSource BeginAnalysis()
        {
            _analysisInvocation = Guid.NewGuid();
            return ImageAlgorithmAnalysisSession.Begin(Context, Context.DocumentInstanceId, Context.ImageRevision, Guid.NewGuid(), _analysisInvocation);
        }

        public void ReleaseAnalysis() => ImageAlgorithmAnalysisSession.Release(Context, _analysisInvocation);

        public void Dispose()
        {
            if (!_windowClosed) Window.Close();
            ReleaseAnalysis();
            View.Dispose();
        }
    }

    private sealed record WindowCase(AlgorithmId AlgorithmId, IAlgorithmParameters Parameters,
        Func<ImageProcessingContext, Window> CreateWindow, string SliderName, double ChangedValue)
    {
        public static WindowCase For(string kind) => kind switch
        {
            "basic" => new(StandardAlgorithmIds.BasicAdjustment, new BasicAdjustmentParameters(), image => new BasicAdjustmentWindow(image), "BrightnessSlider", 10),
            "white-balance" => new(StandardAlgorithmIds.WhiteBalance, new WhiteBalanceParameters(), image => new WhiteBalanceWindow(image), "RedSlider", 1.5),
            "threshold" => new(StandardAlgorithmIds.Threshold, new ThresholdParameters(), image => new ThresholdWindow(image), "ThresholdSlider", 10),
            "edge-detection" => new(StandardAlgorithmIds.Canny, new CannyParameters(), image => new EdgeDetectionWindow(image), "Threshold1Slider", 25),
            "gaussian-blur" => new(StandardAlgorithmIds.GaussianBlur, new GaussianBlurParameters(), image => new GaussianBlurWindow(image), "SigmaSlider", 2),
            "median-blur" => new(StandardAlgorithmIds.MedianBlur, new MedianBlurParameters(), image => new MedianBlurWindow(image), "KernelSizeSlider", 9),
            "morphology" => new(StandardAlgorithmIds.Morphology, new MorphologyParameters(), image => new MorphologyWindow(image), "KernelSlider", 9),
            "denoise" => new(StandardAlgorithmIds.Denoise, new DenoiseParameters(), image => new FilterDenoiseWindow(image), "KernelSlider", 9),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown preview window."),
        };
    }

    private sealed class ImmediateScheduler : IAlgorithmExecutionScheduler
    {
        public ValueTask<AlgorithmResult> ScheduleAsync(AlgorithmProviderMetadata metadata,
            Func<CancellationToken, ValueTask<AlgorithmResult>> operation, CancellationToken cancellationToken)
            => operation(cancellationToken);
    }

    private sealed class ImmediatePreviewProvider(AlgorithmId algorithmId, Task? release = null) : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private PreviewExecutionGate? _nextExecution;

        public int ExecutionCount { get; private set; }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ResultDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AlgorithmImageBuffer? ProducedImage { get; private set; }

        public PreviewExecutionGate GateNextExecution(byte outputPixel)
        {
            Assert.Null(_nextExecution);
            return _nextExecution = new PreviewExecutionGate(outputPixel);
        }

        public AlgorithmProviderMetadata Metadata { get; } = new("test.preview-window", "Window ownership provider",
            AlgorithmProviderKind.Cpu, AlgorithmExecutionPlane.Local, 1,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
            => CanExecuteDescriptor(descriptor, out reason);

        public async ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionCount++;
            PreviewExecutionGate? execution = _nextExecution;
            _nextExecution = null;
            if (execution != null)
            {
                execution.ParameterValue = context.Parameters switch
                {
                    BasicAdjustmentParameters parameters => parameters.Brightness,
                    WhiteBalanceParameters parameters => parameters.RedScale,
                    _ => throw new InvalidOperationException("The gated preview requires a basic or white-balance parameter set."),
                };
                execution.Input = context.Inputs[0].Image;
                execution.Started.TrySetResult(true);
                await execution.Release.Task.ConfigureAwait(false);
            }
            Started.TrySetResult(true);
            if (release != null) await release.ConfigureAwait(false);
            ProducedImage = new AlgorithmImageBuffer(4, 2, 4, AlgorithmImageFormat.Gray8, Enumerable.Repeat(execution?.OutputPixel ?? (byte)77, 8).ToArray());
            if (execution != null) execution.Output = ProducedImage;
            AlgorithmImageArtifact image = new("image", "primary", ProducedImage);
            return new AlgorithmResult
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [image, new ResultDisposalSignal(execution?.ResultDisposed ?? ResultDisposed)],
            };
        }
    }

    private sealed record ResultDisposalSignal(TaskCompletionSource<bool> Signal) : AlgorithmArtifact("test-result-disposal"), IDisposable
    {
        public void Dispose() => Signal.TrySetResult(true);
    }

    private sealed class PreviewExecutionGate(byte outputPixel)
    {
        public byte OutputPixel { get; } = outputPixel;
        public double ParameterValue { get; set; }
        public AlgorithmImageBuffer? Input { get; set; }
        public AlgorithmImageBuffer? Output { get; set; }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ResultDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class AsyncVoidCompletionContext(SynchronizationContext inner) : SynchronizationContext
    {
        private int _activeOperations;
        private int _startedOperations;
        public TaskCompletionSource<bool> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ObservedOperation => Volatile.Read(ref _startedOperations) != 0;

        public override void OperationStarted()
        {
            Interlocked.Increment(ref _startedOperations);
            Interlocked.Increment(ref _activeOperations);
            inner.OperationStarted();
        }

        public override void OperationCompleted()
        {
            try { inner.OperationCompleted(); }
            finally { if (Interlocked.Decrement(ref _activeOperations) == 0) Completed.TrySetResult(true); }
        }

        public override void Post(SendOrPostCallback callback, object? state) => inner.Post(callback, state);
        public override void Send(SendOrPostCallback callback, object? state) => inner.Send(callback, state);
        public override SynchronizationContext CreateCopy() => this;
    }
}
