using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using cvColorVision;
using System.Reflection;

namespace ProjectARVRPro.Services;

// ColorVision 1.4.17.114 exposes fine focus in its camera panel, but has no public command for plugins.
// Bind only those two host commands so this update can ship as a plugin without replacing the host.
internal sealed class LocalVidFocus(
    Func<DeviceCamera, string, TakeImageMode, int, MsgRecord> open,
    Func<DeviceCamera, AutoFocusParam, CancellationToken, bool, MsgRecord> focus)
{
    private static readonly Lazy<LocalVidFocus?> HostCommands = new(BindHostCommands);

    internal static LocalVidFocus ForCamera(DeviceCamera camera)
    {
        if (!camera.RoutesLocally && (!camera.DisplayConfig.UseLocalCamera || camera.DService.DeviceStatus > DeviceStatusType.Closed))
            throw new InvalidOperationException("VID 微聚焦仅支持本地相机。请先关闭服务相机会话，再为所选相机启用本地取图。");
        return HostCommands.Value ?? throw new InvalidOperationException("当前 ColorVision 宿主不支持 VID 微聚焦，请更新宿主。");
    }

    internal MsgRecord Open(DeviceCamera camera) =>
        open(camera, camera.Config.CameraID, camera.Config.TakeImageMode, (int)camera.Config.ImageBpp);

    internal MsgRecord Measure(DeviceCamera camera, AutoFocusParam template, CancellationToken stop)
    {
        if (!camera.RoutesLocally) throw new InvalidOperationException("所选 VID 相机未建立本地相机会话。");
        return focus(camera, template, stop, true);
    }

    private static LocalVidFocus? BindHostCommands()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var openMethod = typeof(DeviceCamera).GetMethod("OpenLocalCamera", flags, [typeof(string), typeof(TakeImageMode), typeof(int)]);
        var focusMethod = typeof(DeviceCamera).GetMethod("AutoFocusLocally", flags, [typeof(AutoFocusParam), typeof(CancellationToken), typeof(bool)]);
        if (openMethod?.ReturnType != typeof(MsgRecord) || focusMethod?.ReturnType != typeof(MsgRecord)) return null;
        return new(openMethod.CreateDelegate<Func<DeviceCamera, string, TakeImageMode, int, MsgRecord>>(),
            focusMethod.CreateDelegate<Func<DeviceCamera, AutoFocusParam, CancellationToken, bool, MsgRecord>>());
    }
}
