using ColorVision.Engine.Templates.Flow;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests
{
    public class STNodeEditorHostTests
    {
        [Fact]
        public void WmGetObject_IsHandledWithoutCreatingAnAutomationProvider()
        {
            RunInSta(() =>
            {
                using var host = new TestSTNodeEditorHost();

                IntPtr result = host.DispatchWmGetObject(out bool handled);

                Assert.True(handled);
                Assert.Equal(IntPtr.Zero, result);
            });
        }

        private static void RunInSta(Action action)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        private sealed class TestSTNodeEditorHost : STNodeEditorHost
        {
            public IntPtr DispatchWmGetObject(out bool handled)
            {
                handled = false;
                return WndProc(IntPtr.Zero, 0x003D, IntPtr.Zero, IntPtr.Zero, ref handled);
            }
        }
    }
}
