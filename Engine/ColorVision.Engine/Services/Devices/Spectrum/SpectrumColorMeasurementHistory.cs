using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Spectrum;

internal readonly record struct SpectrumMeasurementFileRoots(string? DataRoot, string? ServiceDirectory, string WorkingDirectory);

internal sealed class SpectrumColorMeasurementHistory
{
    private readonly DeviceSpectrum device;
    private readonly DeviceConfigurationTarget deviceTarget;
    private readonly MySqlConnectedEndpoint endpoint;
    private readonly MySqlConnectedEndpoint? deviceEndpoint;
    private readonly ConfigSpectrum configuration;
    private readonly string deviceCode;
    private readonly SpectrumMeasurementFileRoots fileRoots;

    private SpectrumColorMeasurementHistory(DeviceSpectrum device, MySqlConnectedEndpoint endpoint)
    {
        this.device = device;
        this.endpoint = endpoint;
        deviceEndpoint = device.ResultDatabaseEndpoint;
        deviceTarget = device.CaptureConfigurationTarget();
        configuration = device.Config;
        deviceCode = configuration.Code;
        fileRoots = device.CaptureSpectrumMeasurementFileRoots();
    }

    internal static SpectrumColorMeasurementHistory Capture(DeviceSpectrum device)
    {
        MySqlConnectedEndpoint? endpoint = SysResourceDao.IsLocalId(device.SysResourceModel.Id)
            ? device.CaptureLocalSpectrumResultDatabase()
            : device.ResultDatabaseEndpoint;
        if (endpoint is not { IsCurrent: true })
            throw new InvalidOperationException("所选光谱仪尚未绑定已连接的结果数据库，请重新选择设备。");
        var history = new SpectrumColorMeasurementHistory(device, endpoint);
        history.RequireCurrent();
        return history;
    }

    internal void RequireCurrent()
    {
        if (!endpoint.IsCurrent || !ReferenceEquals(device.ResultDatabaseEndpoint, deviceEndpoint)
            || !device.IsConfigurationTargetCurrent(deviceTarget) || !ReferenceEquals(device.Config, configuration)
            || !string.Equals(configuration.Code, deviceCode, StringComparison.Ordinal)
            || device.CaptureSpectrumMeasurementFileRoots() != fileRoots)
            throw new InvalidOperationException("光谱仪或结果来源已更改，请重新打开历史结果。");
    }

    internal async Task<IReadOnlyList<SpectrumColorMeasurementSummary>> ReadRecentAsync()
    {
        RequireCurrent();
        using var db = endpoint.CreateDbClient();
        var results = await db.Queryable<SpectumResultEntity>()
            .Where(item => item.DeviceCode == deviceCode && !item.DataType)
            .OrderBy(item => item.Id, OrderByType.Desc).Take(100)
            .Select(item => new SpectrumColorMeasurementSummary
            {
                ResultId = item.Id, CapturedAt = item.CreateDate, Y = item.fPh,
                CieX = item.fx, CieY = item.fy, PeakAd = item.fIp, NdPort = item.NDPort,
            }).ToListAsync();
        RequireCurrent();
        return results;
    }

    internal async Task<SpectrumColorMeasurement> LoadAsync(int resultId)
    {
        RequireCurrent();
        using var db = endpoint.CreateDbClient();
        SpectumResultEntity? entity = await db.Queryable<SpectumResultEntity>()
            .Where(item => item.Id == resultId && item.DeviceCode == deviceCode && !item.DataType).FirstAsync();
        RequireCurrent();
        if (entity == null)
            throw new InvalidOperationException("未找到所选光谱仪的亮度测量结果，请重新选择。");
        SpectrumColorMeasurement result = DeviceSpectrum.CreateColorMeasurement(entity, fileRoots);
        RequireCurrent();
        return result;
    }
}
