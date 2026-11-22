using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process;
using System.IO;

namespace ProjectARVRPro.Services;

internal static class LuxConfigurationProtocol
{
    private const string ResultSavePath = "ResultSavePath";
    private static readonly ILog log = LogManager.GetLogger(typeof(LuxConfigurationProtocol));

    internal static string Handle(string text, ViewResultManagerConfig config, Func<bool> isBusy, Action persist)
    {
        JObject response;
        try
        {
            var request = JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            response = (string?)request["method"] switch
            {
                "get" => Get(request["param"], config),
                "set" => Set(request, config, isBusy, persist),
                _ => Status("method 必须为 get 或 set。")
            };
        }
        catch (JsonException)
        {
            response = Status("JSON 请求格式无效。");
        }
        catch (Exception ex)
        {
            log.Error("LUX 配置请求失败。", ex);
            response = Status("LUX 配置请求失败，请查看服务日志。");
        }
        return response.ToString(Formatting.None);
    }

    private static JObject Get(JToken? parameters, ViewResultManagerConfig config)
    {
        if (parameters is not JArray { Count: > 0 } keys) return Status("param 必须为非空参数名数组。");

        JObject response = Status();
        foreach (JToken token in keys)
        {
            if (token.Type != JTokenType.String) return Status("param 必须为非空参数名数组。");
            string key = token.Value<string>()!;
            JToken? value = key switch
            {
                ResultSavePath => new JValue(config.CsvSavePath),
                "camera_exposure" => new JValue(0),
                "SaveImage" => new JValue(string.Empty),
                _ => null
            };
            if (value == null) return Status($"未知参数：{key}");
            response[key] = value;
        }
        return response;
    }

    private static JObject Set(JObject request, ViewResultManagerConfig config, Func<bool> isBusy, Action persist)
    {
        if (request.Count != 2 || request[ResultSavePath] is not JValue { Value: string path }
            || !Path.IsPathFullyQualified(path))
            return Status("set 需要 ResultSavePath，值为绝对目录或 UNC 路径。");

        const string property = nameof(ViewResultManagerConfig.CsvSavePath);
        if (ProjectSettingsSession.ValidateValue(config, property, path) is { } error) return Status(error);

        string directory = Path.GetFullPath(path);
        string previous = config.CsvSavePath;
        if (string.Equals(previous, directory, StringComparison.Ordinal)) return Status();
        if (isBusy()) return Status("测试正在执行或保存结果，请在结束后再设置目录。");

        try
        {
            config.CsvSavePath = directory;
            persist();
        }
        catch
        {
            config.CsvSavePath = previous;
            throw;
        }

        ProjectConfigurationLog.Saved(null, ProcessManager.CurrentActiveGroupName, property, previous, directory);
        return Status();
    }

    private static JObject Status(string? reason = null) => new()
    {
        ["status"] = reason == null ? 1 : 0,
        ["reson"] = reason ?? string.Empty
    };
}
