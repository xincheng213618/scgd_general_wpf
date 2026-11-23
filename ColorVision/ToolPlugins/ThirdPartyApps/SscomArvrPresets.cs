using ColorVision.SocketProtocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace ColorVision.ToolPlugins.ThirdPartyApps;

internal static class SscomArvrPresets
{
    internal static IReadOnlyList<SscomCommandPreset> Build(string serialNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
        string sn = serialNumber.Trim();
        return
        [
            Create("查询流程启用状态 GetProcessEnable", "GetProcessEnable"),
            Create("初始化测试 ProjectARVRInit", "ProjectARVRInit", sn),
            Create("运行全部测试 RunAll", "RunAll", sn),
            Create("测量 VID MeasureVID", "MeasureVID", sn),
        ];
    }

    private static SscomCommandPreset Create(string name, string eventName, string? serialNumber = null)
    {
        var request = new SocketRequest
        {
            Version = "1.0",
            MsgID = Guid.NewGuid().ToString("N"),
            EventName = eventName,
        };
        if (serialNumber != null) request.SerialNumber = serialNumber;

        return new SscomCommandPreset(name, Encoding.UTF8.GetBytes(request.ToString()));
    }
}
