using WindowsServicePlugin.CVWinSMS;
using System.Reflection;

namespace ColorVision.UI.Tests;

public class InstallToolAsyncCommandTests
{
    [Fact]
    public async Task MenuBoundaryObservesFailureThrownAfterAwait()
    {
        var expected = new InvalidOperationException("invalid version");
        Exception? reported = null;

        MethodInfo boundary = typeof(InstallTool).GetMethod(
            "ExecuteMenuActionAsync",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Func<Task> failAfterAwait = async () =>
        {
            await Task.Yield();
            throw expected;
        };
        Action<Exception> reportFailure = ex => reported = ex;

        await (Task)boundary.Invoke(null, [failAfterAwait, reportFailure])!;

        Assert.Same(expected, reported);
        Assert.Equal(
            typeof(Task),
            typeof(InstallTool).GetMethod(nameof(InstallTool.Download))!.ReturnType);
    }
}
