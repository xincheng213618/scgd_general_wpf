using ColorVision.UI;
using log4net;
using Newtonsoft.Json;

namespace ProjectARVRPro;

internal static class ProjectConfigurationLog
{
    private static readonly ILog log = LogManager.GetLogger(typeof(ProjectConfigurationLog));

    internal static void Saved(string? station, string? flowGroup, string property, object? oldValue, object? newValue)
    {
        if (!log.IsInfoEnabled || Equals(oldValue, newValue)) return;

        log.Info(JsonConvert.SerializeObject(new
        {
            Event = "ArvrConfigurationSaved",
            Timestamp = DateTimeOffset.Now.ToString("O"),
            Machine = Environment.MachineName,
            Station = station ?? ReadStation(),
            FlowGroup = flowGroup,
            Property = property,
            OldValue = oldValue is Enum oldOption ? oldOption.ToString() : oldValue,
            NewValue = newValue is Enum newOption ? newOption.ToString() : newValue
        }));
    }

    private static string? ReadStation()
    {
        try { return ConfigService.Instance?.GetRequiredService<ProjectARVRProConfig>().LuxMachineNo; }
        catch (Exception ex)
        {
            log.Debug("无法读取 ARVRPro 日志的 LUX 工位信息。", ex);
            return null;
        }
    }
}
