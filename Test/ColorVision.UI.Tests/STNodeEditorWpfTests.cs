using ST.Library.UI.NodeEditor;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public class STNodeEditorWpfTests
{
    [Fact]
    public void PublicEditorSurfaces_AreNativeWpfControls()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            using var propertyGrid = new STNodePropertyGrid();
            using var treeView = new STNodeTreeView();
            using var panel = new STNodeEditorPannel();

            Assert.IsAssignableFrom<Control>(editor);
            Assert.IsAssignableFrom<Control>(propertyGrid);
            Assert.IsAssignableFrom<Control>(treeView);
            Assert.IsAssignableFrom<Control>(panel);
            Assert.DoesNotContain(
                editor.GetType().Assembly.GetReferencedAssemblies(),
                reference => reference.Name == "System.Windows.Forms");
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
}
