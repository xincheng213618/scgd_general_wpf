using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Algorithm;

internal static class LocalAlgorithmResultDirectory
{
    internal static string Resolve(string configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory)) return Path.GetFullPath(configuredDirectory.Trim());
        var configurations = ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().Select(device => device.Config).ToArray();
        return Resolve(configuredDirectory, configurations, DateTime.Now);
    }

    internal static string Resolve(string configuredDirectory, IEnumerable<ConfigAlgorithm> configurations, DateTime date)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory)) return Path.GetFullPath(configuredDirectory.Trim());
        var candidates = configurations.ToArray();
        var defaults = candidates.Where(config => string.Equals(config.Code, "DEV.Algorithm.Default", StringComparison.OrdinalIgnoreCase)).ToArray();
        ConfigAlgorithm config = defaults.Length == 1 ? defaults[0]
            : candidates.Length == 1 ? candidates[0]
            : throw new InvalidOperationException(candidates.Length == 0
                ? "未找到算法服务，无法确定结果目录；请配置算法服务或填写结果目录。"
                : "存在多个算法服务且无法确定默认服务，请填写结果目录。");
        string root = config.FileServerCfg?.DataBasePath ?? "";
        string code = config.Code ?? "";
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException("算法服务的数据基础路径必须是完整路径。");
        if (string.IsNullOrWhiteSpace(code) || code is "." or ".." || code.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("算法服务标识不能用于结果目录。");
        return Path.GetFullPath(Path.Combine(root, code, "Data", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }
}
