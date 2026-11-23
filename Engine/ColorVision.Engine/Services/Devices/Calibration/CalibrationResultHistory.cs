using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Devices.Calibration;

internal static class CalibrationResultHistory
{
    internal static ISugarQueryable<MeasureResultImgModel> Query(SqlSugarClient db, string calibrationDeviceCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(calibrationDeviceCode);
        // Local Flow rows retain the camera's DeviceCode for downstream image consumers.
        // A recorded calibration owner takes precedence over that camera code.
        bool sqlite = db.CurrentConnectionConfig.DbType == DbType.Sqlite;
        const string marker = "JSON_EXTRACT(`params`, '$.CalibrationDeviceCode')";
        string markerType = sqlite ? "JSON_TYPE(`params`, '$.CalibrationDeviceCode') = 'text'" : $"JSON_TYPE({marker}) = 'STRING'";
        string owner = sqlite ? marker : $"JSON_UNQUOTE({marker})";
        string recordedOwner = $"CASE WHEN JSON_VALID(`params`) THEN CASE WHEN {markerType} THEN {owner} END END";
        string resultOwner = $"COALESCE(NULLIF({recordedOwner}, ''), `device_code`)";
        string predicate = sqlite ? $"{resultOwner} COLLATE BINARY = @calibrationDeviceCode" : $"CAST({resultOwner} AS BINARY) = @calibrationDeviceCode";
        return db.Queryable<MeasureResultImgModel>().Where(predicate, new { calibrationDeviceCode });
    }
}
