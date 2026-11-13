using ColorVision.Engine.Services.Devices.Camera.Local;

namespace ColorVision.Engine.Services.Devices.Camera.Controls
{
    // Project actual ownership into one UI state. A preference never replaces an open owner.
    internal sealed record CameraPanelState(bool Connected, bool Busy, bool CanConnect, bool ShowPreview, bool CanPreview,
        bool ShowCapture, bool CanCapture, bool ShowParameters, bool ShowServiceSettings, string StatusKey)
    {
        internal static CameraPanelState Create(CameraBackendState backend, bool previewing, bool operating)
        {
            DeviceStatusType status = backend.Status;
            bool ready = status is DeviceStatusType.Opened or DeviceStatusType.LiveOpened or DeviceStatusType.Free;
            bool busy = operating || backend.IsCommandRunning || status is DeviceStatusType.Opening or DeviceStatusType.Closing or DeviceStatusType.Busy;
            bool connected = backend.LocalOwned || backend.VideoOwned || backend.ServiceMayOwnCamera || ready || status == DeviceStatusType.Busy;
            bool canOpen = !connected && (status == DeviceStatusType.Closed || backend.OpensLocally);
            bool canConnect = !busy && canOpen;
            // Local capture and preview share a session. Legacy service/video owners must close first.
            bool showPreview = !backend.VideoOwned && ((backend.LocalOwned && (ready || status == DeviceStatusType.Busy)) || canOpen);
            bool canPreview = !busy && showPreview;
            bool showCapture = (ready || status == DeviceStatusType.Busy) && !backend.VideoOwned;
            bool canCapture = !busy && showCapture;
            string key = busy ? "CameraPanel_Working" : previewing ? "CameraPanel_Previewing"
                : backend.LocalOwned && status == DeviceStatusType.Unknown ? "DeviceStatusUnknown"
                : backend.LocalOwned || backend.VideoOwned || ready ? "CameraPanel_Ready" : canConnect ? "CameraPanel_Disconnected" : status switch
                {
                    DeviceStatusType.OffLine => "DeviceStatusOffLine",
                    DeviceStatusType.Unauthorized => "DeviceStatusUnauthorized",
                    DeviceStatusType.UnInit => "DeviceStatusUnInit",
                    _ => "DeviceStatusUnknown"
                };
            return new(connected, busy, canConnect, showPreview, canPreview, showCapture, canCapture, connected,
                !backend.LocalOwned && !backend.VideoOwned, key);
        }
    }
}
