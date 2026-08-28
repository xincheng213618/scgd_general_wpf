using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using System;
using System.Threading;
using System.Windows;
using Xunit;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmAnalysisWindowOwnershipTests
{
    [Fact]
    public void OwnerClosingProgressWindowCancelsTheRunningAnalysis()
    {
        WpfTestHost.Invoke(() =>
        {
            using CancellationTokenSource cancellation = new();
            Window ownerWindow = new() { Width = 100, Height = 100 };
            ImageAlgorithmProgressWindow progressWindow = new("analysis", cancellation);
            try
            {
                ownerWindow.Show();
                progressWindow.Owner = ownerWindow;
                progressWindow.Show();

                ownerWindow.Close();

                Assert.True(cancellation.IsCancellationRequested);
                Assert.True(progressWindow.WasCancelled);
            }
            finally
            {
                progressWindow.Complete();
                ownerWindow.Close();
            }
        });
    }

    [Fact]
    public void CapturedOwnerDoesNotDriftAndClosingItInvalidatesPresentation()
    {
        WpfTestHost.Invoke(() =>
        {
            _ = Application.Current ?? new Application();
            Window initiating = new() { Width = 100, Height = 100 };
            Window laterActive = new() { Width = 100, Height = 100 };
            Window firstResult = new();
            Window secondResult = new();
            try
            {
                initiating.Show();
                AlgorithmAnalysisWindowOwner owner = AlgorithmAnalysisWindowOwner.From(initiating);
                laterActive.Show();

                Assert.True(owner.TryAssign(firstResult));
                Assert.Same(initiating, firstResult.Owner);
                Assert.NotSame(laterActive, firstResult.Owner);

                initiating.Close();
                Assert.False(owner.TryAssign(secondResult));
                Assert.Null(secondResult.Owner);
            }
            finally
            {
                firstResult.Close();
                secondResult.Close();
                laterActive.Close();
                initiating.Close();
            }
        });
    }

    [Fact]
    public void ShowFailureDisposesWindowResultAndReleasesRegisteredSession()
    {
        WpfTestHost.Invoke(() =>
        {
            Window ownerWindow = new();
            ownerWindow.Show();
            AlgorithmResult result = Result();
            DisposableWindow? created = null;
            bool released = false;

            bool shown = AlgorithmAnalysisResultWindowTransaction.TryShow(
                result,
                AlgorithmAnalysisWindowOwner.From(ownerWindow),
                value => created = new DisposableWindow(value),
                _ => true,
                () => released = true,
                previousWindow: null,
                out Exception? failure,
                _ => throw new InvalidOperationException("show failed"));

            Assert.False(shown);
            Assert.IsType<InvalidOperationException>(failure);
            Assert.True(released);
            Assert.NotNull(created);
            Assert.True(created.IsDisposed);
            Assert.True(result.IsDisposed);
            ownerWindow.Close();
        });
    }

    [Fact]
    public void ConstructorOrRegistrationFailureLeavesNoResultOwnershipBehind()
    {
        WpfTestHost.Invoke(() =>
        {
            Window ownerWindow = new();
            ownerWindow.Show();
            AlgorithmAnalysisWindowOwner owner = AlgorithmAnalysisWindowOwner.From(ownerWindow);
            AlgorithmResult constructorResult = Result();
            int constructorReleaseCount = 0;
            bool constructed = AlgorithmAnalysisResultWindowTransaction.TryShow(
                constructorResult,
                owner,
                _ => throw new InvalidOperationException("constructor failed"),
                _ => true,
                () => constructorReleaseCount++,
                previousWindow: null,
                out Exception? constructorFailure);
            Assert.False(constructed);
            Assert.IsType<InvalidOperationException>(constructorFailure);
            Assert.Equal(1, constructorReleaseCount);
            Assert.True(constructorResult.IsDisposed);

            AlgorithmResult registrationResult = Result();
            DisposableWindow? rejectedWindow = null;
            int registrationReleaseCount = 0;
            bool registered = AlgorithmAnalysisResultWindowTransaction.TryShow(
                registrationResult,
                owner,
                value => rejectedWindow = new DisposableWindow(value),
                _ => false,
                () => registrationReleaseCount++,
                previousWindow: null,
                out Exception? registrationFailure);
            Assert.False(registered);
            Assert.Null(registrationFailure);
            Assert.Equal(1, registrationReleaseCount);
            Assert.NotNull(rejectedWindow);
            Assert.True(rejectedWindow.IsDisposed);
            Assert.True(registrationResult.IsDisposed);
            ownerWindow.Close();
        });
    }

    [Fact]
    public void FailedPresentationRunsEveryCleanupWhenEarlierCleanupThrows()
    {
        WpfTestHost.Invoke(() =>
        {
            Window ownerWindow = new();
            ownerWindow.Show();
            AlgorithmResult result = Result();
            ThrowingDisposableWindow? created = null;
            bool releaseAttempted = false;

            bool shown = AlgorithmAnalysisResultWindowTransaction.TryShow(
                result,
                AlgorithmAnalysisWindowOwner.From(ownerWindow),
                _ => created = new ThrowingDisposableWindow(),
                _ => true,
                () =>
                {
                    releaseAttempted = true;
                    throw new InvalidOperationException("release failed");
                },
                previousWindow: null,
                out Exception? failure,
                _ => throw new InvalidOperationException("show failed"));

            Assert.False(shown);
            Assert.Equal("show failed", failure?.Message);
            Assert.True(releaseAttempted);
            Assert.NotNull(created);
            Assert.True(created.DisposeAttempted);
            Assert.True(result.IsDisposed);
            ownerWindow.Close();
        });
    }

    [Fact]
    public void SupersededWindowCloseFailureRollsBackTheNewPresentation()
    {
        WpfTestHost.Invoke(() =>
        {
            Window ownerWindow = new();
            ownerWindow.Show();
            AlgorithmResult result = Result();
            ThrowingDisposableWindow previous = new();
            bool created = false;
            bool released = false;

            bool shown = AlgorithmAnalysisResultWindowTransaction.TryShow(
                result,
                AlgorithmAnalysisWindowOwner.From(ownerWindow),
                _ =>
                {
                    created = true;
                    return new Window();
                },
                _ => true,
                () => released = true,
                previous,
                out Exception? failure);

            Assert.False(shown);
            Assert.IsType<InvalidOperationException>(failure);
            Assert.False(created);
            Assert.True(released);
            Assert.True(previous.DisposeAttempted);
            Assert.True(result.IsDisposed);
            ownerWindow.Close();
        });
    }

    private static AlgorithmResult Result() => new()
    {
        InvocationId = Guid.NewGuid(),
        AlgorithmId = new AlgorithmId("test.analysis.window-ownership"),
        AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
        Status = AlgorithmResultStatus.Succeeded,
    };

    private sealed class DisposableWindow(AlgorithmResult result) : Window, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            result.Dispose();
            Close();
        }
    }

    private sealed class ThrowingDisposableWindow : Window, IDisposable
    {
        public bool DisposeAttempted { get; private set; }

        public void Dispose()
        {
            DisposeAttempted = true;
            throw new InvalidOperationException("window cleanup failed");
        }
    }
}
