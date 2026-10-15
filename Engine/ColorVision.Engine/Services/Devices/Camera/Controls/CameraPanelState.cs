using ColorVision.Engine.Services.Devices.Camera.Local;

namespace ColorVision.Engine.Services.Devices.Camera.Controls
{
    // Project actual ownership into one UI state. A preference never replaces an open owner.
    internal sealed record CameraPanelState(bool Connected, bool Busy, bool CanConnect, bool CanPreview,
        bool CanCapture, bool ShowParameters, bool ShowServiceSettings, string StatusKey)
    {
        internal static CameraPanelState Create(CameraBackendState backend, bool previewing, bool operating)
        {
            DeviceStatusType status = backend.Status;
            bool ready = status is DeviceStatusType.Opened or DeviceStatusType.LiveOpened or DeviceStatusType.Free;
            bool busy = operating || status is DeviceStatusType.Opening or DeviceStatusType.Closing or DeviceStatusType.Busy;
            bool connected = backend.LocalOwned || backend.VideoOwned || backend.ServiceMayOwnCamera || ready || status == DeviceStatusType.Busy;
            bool canConnect = !connected && !busy && (status == DeviceStatusType.Closed || backend.OpensLocally);
            // Local capture and preview share a session. Legacy service/video owners must close first.
            bool canPreview = !busy && !backend.VideoOwned && (backend.LocalOwned || canConnect);
            bool canCapture = !busy && ready && !backend.VideoOwned;
            string key = busy ? "CameraPanel_Working" : previewing ? "CameraPanel_Previewing"
                : backend.LocalOwned || backend.VideoOwned || ready ? "CameraPanel_Ready" : canConnect ? "CameraPanel_Disconnected" : status switch
                {
                    DeviceStatusType.OffLine => "DeviceStatusOffLine",
                    DeviceStatusType.Unauthorized => "DeviceStatusUnauthorized",
                    DeviceStatusType.UnInit => "DeviceStatusUnInit",
                    _ => "DeviceStatusUnknown"
                };
            return new(connected, busy, canConnect, canPreview, canCapture, connected,
                !backend.LocalOwned && !backend.VideoOwned, key);
        }
    }
}
