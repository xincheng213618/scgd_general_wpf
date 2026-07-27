using System.Reflection;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public class STNodeTypeRegistryConcurrencyTests
{
    [Fact]
    public void AssemblyLoadCallbackDoesNotWaitForRegistryLock()
    {
        Type registryType = typeof(STNode).Assembly.GetType(
            "ST.Library.UI.NodeEditor.STNodeTypeRegistry",
            throwOnError: true)!;
        object syncRoot = registryType
            .GetField("SyncRoot", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        MethodInfo assemblyLoadCallback = registryType.GetMethod(
            "CurrentDomain_AssemblyLoad",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        using var callbackStarted = new ManualResetEventSlim();
        Exception? callbackException = null;

        var callbackThread = new Thread(() =>
        {
            callbackStarted.Set();
            try
            {
                assemblyLoadCallback.Invoke(
                    null,
                    new object?[]
                    {
                        null,
                        new AssemblyLoadEventArgs(typeof(STNode).Assembly),
                    });
            }
            catch (Exception ex)
            {
                callbackException = ex;
            }
        })
        {
            IsBackground = true,
        };

        bool completedWhileLocked;
        lock (syncRoot)
        {
            callbackThread.Start();
            Assert.True(callbackStarted.Wait(TimeSpan.FromSeconds(5)));
            completedWhileLocked = callbackThread.Join(TimeSpan.FromSeconds(5));
        }

        if (!completedWhileLocked)
        {
            callbackThread.Join(TimeSpan.FromSeconds(5));
        }
        Assert.True(
            completedWhileLocked,
            "AssemblyLoad callback waited for the node type registry lock.");
        Assert.Null(callbackException);
    }
}
