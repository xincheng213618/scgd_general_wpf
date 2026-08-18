using ProjectARVRPro.Process;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class ProcessStepProjectionTests
{
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
}
