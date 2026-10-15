using ProjectARVRPro.Process;
using ColorVision.UI.Controls;
using ProjectARVRPro.Controls;
using ColorVision.Testing;
using System.Windows;
using System.Windows.Controls;

namespace ProjectARVRPro.Tests;

public sealed class ProcessStepProjectionTests
{
    [Fact]
    public void StepsShareAvailableWidthAfterAddingStepsResizingAndAdvancing()
    {
        StaTest.Run(() =>
        {
            var progress = new ProcessStepProgress();
            var metas = Enumerable.Range(1, 12).Select(index => new ProcessMeta { Name = $"Step {index} " + new string('W', 80) }).ToArray();
            var items = (ItemsControl)progress.FindName("StepItems");

            void AssertLayout(double width)
            {
                progress.Measure(new Size(width, progress.Height));
                progress.Arrange(new Rect(0, 0, width, progress.Height));
                progress.UpdateLayout();
                Assert.Equal(width, items.ActualWidth);
                for (int index = 0; index < progress.Steps.Count; index++)
                {
                    var container = Assert.IsAssignableFrom<FrameworkElement>(items.ItemContainerGenerator.ContainerFromIndex(index));
                    double stepWidth = width / progress.Steps.Count;
                    Assert.Equal(stepWidth, container.ActualWidth);
                    Assert.Equal(index * stepWidth, container.TranslatePoint(new Point(), progress).X);
                }
            }

            progress.Rebuild(metas.Take(2));
            AssertLayout(720);
            progress.Rebuild(metas);
            AssertLayout(720);
            AssertLayout(360);
            progress.Start(metas.Length - 1);
            AssertLayout(360);
            progress.Reset();
            progress.Rebuild(metas.Take(2));
            AssertLayout(360);
        });
    }

    [Fact]
    public void GetEnabledStepIndexMapsOriginalIndexesToVisibleIndexes()
    {
        var processMetas = new List<ProcessMeta>
        {
            new() { Name = "Disabled first", IsEnabled = false },
            new() { Name = "First visible", IsEnabled = true },
            new() { Name = "Disabled middle", IsEnabled = false },
            new() { Name = "Second visible", IsEnabled = true },
        };

        Assert.Equal(0, ProcessManager.GetEnabledStepIndex(processMetas, 1));
        Assert.Equal(1, ProcessManager.GetEnabledStepIndex(processMetas, 3));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void GetEnabledStepIndexRejectsDisabledOrOutOfRangeIndexes(int processIndex)
    {
        var processMetas = new List<ProcessMeta>
        {
            new() { IsEnabled = false },
            new() { IsEnabled = true },
            new() { IsEnabled = false },
            new() { IsEnabled = true },
        };

        Assert.Equal(-1, ProcessManager.GetEnabledStepIndex(processMetas, processIndex));
    }

    [Fact]
    public void ProgressEstimatesStayBelowCompletionAndUnknownDurationRemainsIndeterminate()
    {
        var step = new ProcessStepProgressItem(new ProcessMeta { Name = "Test", FlowTemplate = "Flow" }, 1, true, true);
        step.Start();
        step.UpdateElapsed(100, 0);
        Assert.True(double.IsNaN(step.Progress));
        step.UpdateElapsed(400, 1000);
        Assert.Equal(40, step.Progress);
        step.UpdateElapsed(2000, 1000);
        Assert.Equal(99, step.Progress);
        Assert.Equal(CircularProgressState.Running, step.State);
        step.Finish(true);
        Assert.Equal(100, step.Progress);
        Assert.Equal(1, step.Number);
        step.UpdateElapsed(3000, 1000);
        Assert.Equal(CircularProgressState.Completed, step.State);
        Assert.Equal(100, step.Progress);
        step.Reset();
        Assert.Equal(CircularProgressState.Waiting, step.State);
        Assert.True(double.IsNaN(step.Progress));
    }

    [Fact]
    public void AdvancingProgressPreservesFailedStepsAndDoesNotCompleteUnexecutedSteps()
    {
        StaTest.Run(() =>
        {
            var disabled = new ProcessMeta { Name = "Disabled", IsEnabled = false };
            var first = new ProcessMeta { Name = "Failed", FlowTemplate = "Shared" };
            var second = new ProcessMeta { Name = "Next", FlowTemplate = "Shared" };
            var third = new ProcessMeta { Name = "Unexecuted" };
            var progress = new ProcessStepProgress();
            progress.Rebuild(new[] { disabled, first, second, third });
            Assert.Equal(3, progress.Steps.Count);
            Assert.Same(first, progress.Steps[0].Meta);
            Assert.Same(second, progress.Steps[1].Meta);
            Assert.Equal(2, progress.Steps[1].Number);
            progress.Start(0);
            progress.UpdateElapsed(0, 250, 1000);
            progress.Finish(0, false);
            progress.Start(1);
            progress.Finish(1, true);
            Assert.Equal(CircularProgressState.Failed, progress.Steps[0].State);
            Assert.Equal(25, progress.Steps[0].Progress);
            Assert.Equal(CircularProgressState.Completed, progress.Steps[1].State);
            Assert.Equal(CircularProgressState.Waiting, progress.Steps[2].State);
            progress.Reset();
            Assert.All(progress.Steps, step => Assert.Equal(CircularProgressState.Waiting, step.State));
        });
    }
}
