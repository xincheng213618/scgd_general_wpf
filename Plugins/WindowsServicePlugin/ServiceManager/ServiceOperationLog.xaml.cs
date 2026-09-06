using ColorVision.UI.LogImp;
using System.Windows.Controls;

namespace WindowsServicePlugin.ServiceManager;

public partial class ServiceOperationLog : UserControl, IDisposable
{
    private ModuleLogViewerBinder? _logBinder;

    public ServiceOperationLog() => InitializeComponent();

    // Capture belongs to the window lifetime, including time spent on other tabs.
    public void StartCapture()
    {
        _logBinder ??= new ModuleLogViewerBinder(LogViewer, "WindowsServicePlugin.ServiceManager");
    }

    public void Dispose()
    {
        _logBinder?.Dispose();
        _logBinder = null;
        GC.SuppressFinalize(this);
    }
}
