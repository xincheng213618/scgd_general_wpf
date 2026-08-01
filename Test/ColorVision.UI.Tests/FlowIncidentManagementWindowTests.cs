using ColorVision.Engine.FlowProcessing.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class FlowIncidentManagementWindowTests
{
    [Fact]
    public void WindowExposesFiltersDetailsActionsAndRunEvidence()
    {
        RunInSta(() =>
        {
            var window = new FlowIncidentManagementWindow(
                focusFlowNode: null,
                autoLoad: false);
            try
            {
                Assert.IsType<ComboBox>(
                    window.FindName("StateFilterComboBox"));
                Assert.IsType<TextBox>(
                    window.FindName("SeverityFilterTextBox"));
                Assert.IsType<TextBox>(
                    window.FindName("KindFilterTextBox"));
                Assert.IsType<TextBox>(
                    window.FindName("SearchTextBox"));
                Assert.IsType<DataGrid>(
                    window.FindName("IncidentDataGrid"));
                Assert.IsType<DataGrid>(
                    window.FindName("EventDataGrid"));
                Assert.IsType<DataGrid>(
                    window.FindName("AttemptDataGrid"));

                var acknowledgeButton = Assert.IsType<Button>(
                    window.FindName("AcknowledgeButton"));
                var resolveButton = Assert.IsType<Button>(
                    window.FindName("ResolveButton"));
                var runAnalysisButton = Assert.IsType<Button>(
                    window.FindName("OpenRunAnalysisButton"));
                var locateButton = Assert.IsType<Button>(
                    window.FindName("LocateFlowNodeButton"));
                Assert.False(acknowledgeButton.IsEnabled);
                Assert.False(resolveButton.IsEnabled);
                Assert.False(runAnalysisButton.IsEnabled);
                Assert.False(locateButton.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(
            thread.Join(TimeSpan.FromSeconds(10)),
            "Incident management window test did not finish.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
