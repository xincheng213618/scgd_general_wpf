using ColorVision.Database;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    internal sealed record LumFourColorRecentImage(int Id, string DeviceCode, DateTime? CapturedAt, string FilePath, int ResultCode)
    {
        public string DisplayName => $"{CapturedAt:MM-dd HH:mm:ss} · #{Id} · {Path.GetFileName(FilePath)}";
    }

    internal static class LumFourColorRecentImages
    {
        public static Task<IReadOnlyList<LumFourColorRecentImage>> QueryAsync(string deviceCode) => Task.Run<IReadOnlyList<LumFourColorRecentImage>>(() =>
        {
            if (string.IsNullOrWhiteSpace(deviceCode)) throw new InvalidOperationException("请先选择相机。");
            if (!MySqlControl.GetInstance().IsConnect) throw new InvalidOperationException("数据库未连接，无法读取最近图像。可使用图像导入。");
            using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = DbType.MySql, IsAutoCloseConnection = true });
            var records = db.Queryable<MeasureResultImgModel>().Where(row => row.DeviceCode == deviceCode)
                .OrderBy(row => row.CreateDate, OrderByType.Desc).OrderBy(row => row.Id, OrderByType.Desc).Take(100).ToList();
            return CreateList(records, deviceCode);
        });

        internal static IReadOnlyList<LumFourColorRecentImage> CreateList(IEnumerable<MeasureResultImgModel> records, string deviceCode) => records
            .Where(row => !string.IsNullOrWhiteSpace(deviceCode) && string.Equals(row.DeviceCode, deviceCode, StringComparison.Ordinal))
            .OrderByDescending(row => row.CreateDate).ThenByDescending(row => row.Id).Take(100)
            .Select(row => new LumFourColorRecentImage(row.Id, deviceCode, row.CreateDate, ResolvePath(row), row.ResultCode)).ToArray();

        private static string ResolvePath(MeasureResultImgModel row)
        {
            // Only use paths carried by this record. Do not guess neighbouring files or fall back to an older capture.
            string[] paths = [row.FileUrl ?? string.Empty, row.RawFile ?? string.Empty];
            return paths.FirstOrDefault(path => string.Equals(Path.GetExtension(path), ".cvcie", StringComparison.OrdinalIgnoreCase))
                ?? paths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;
        }

        public static LumFourColorCieCapture Load(LumFourColorRecentImage image, string deviceCode)
        {
            if (!string.Equals(image.DeviceCode, deviceCode, StringComparison.Ordinal))
                throw new InvalidOperationException("图像不属于当前相机，请刷新后重新选择。");
            if (image.ResultCode != 0) throw new InvalidOperationException("该次拍摄失败，请选择成功的图像。");
            if (string.IsNullOrWhiteSpace(image.FilePath) || !File.Exists(image.FilePath))
                throw new InvalidOperationException("该图像文件不存在，请重新选择或使用图像导入。");
            var frame = LumFourColorCieService.Load(image.FilePath);
            // A database result does not establish which calibration coefficients were used.
            return frame with { Source = $"{image.DeviceCode} · #{image.Id} · {image.CapturedAt:yyyy-MM-dd HH:mm:ss}\n{frame.Source}" };
        }
    }
}
