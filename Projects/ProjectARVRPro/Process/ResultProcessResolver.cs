using log4net;
using Newtonsoft.Json;

namespace ProjectARVRPro.Process
{
    /// <summary>
    /// Captures and restores the process required to replay a persisted result.
    /// </summary>
    public static class ResultProcessResolver
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ResultProcessResolver));

        public static void Capture(ProjectARVRReuslt result, IProcess? process)
        {
            ArgumentNullException.ThrowIfNull(result);

            result.ProcessTypeFullName = process?.GetType().FullName;
            result.ProcessConfigJson = null;
            if (process?.GetProcessConfig() is not object config)
                return;

            try
            {
                result.ProcessConfigJson = JsonConvert.SerializeObject(config);
            }
            catch (Exception ex)
            {
                log.Warn($"保存结果解析配置快照失败: {result.ProcessTypeFullName}", ex);
            }
        }

        public static IProcess? Resolve(
            ProjectARVRReuslt result,
            IEnumerable<IProcess> processTemplates,
            IEnumerable<ProcessMeta> legacyProcessMetas)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(processTemplates);
            ArgumentNullException.ThrowIfNull(legacyProcessMetas);

            if (!string.IsNullOrWhiteSpace(result.ProcessTypeFullName))
            {
                IProcess? process = CreateProcess(result.ProcessTypeFullName, result.ProcessConfigJson, processTemplates);
                if (process != null)
                    return process;

                log.Warn($"历史结果解析器不可用: {result.ProcessTypeFullName}; 尝试按模板 {result.Model} 兼容解析");
            }

            return legacyProcessMetas
                .FirstOrDefault(meta => string.Equals(meta.FlowTemplate, result.Model, StringComparison.OrdinalIgnoreCase))
                ?.Process;
        }

        private static IProcess? CreateProcess(
            string processTypeFullName,
            string? processConfigJson,
            IEnumerable<IProcess> processTemplates)
        {
            IProcess[] templates = processTemplates as IProcess[] ?? processTemplates.ToArray();
            IProcess? template = templates.FirstOrDefault(
                process => string.Equals(process.GetType().FullName, processTypeFullName, StringComparison.Ordinal));

            if (template == null)
            {
                string className = GetClassName(processTypeFullName);
                IProcess[] matches = templates
                    .Where(process => string.Equals(process.GetType().Name, className, StringComparison.Ordinal))
                    .Take(2)
                    .ToArray();
                template = matches.Length == 1 ? matches[0] : null;
            }

            IProcess? processInstance = template?.CreateInstance();
            if (processInstance == null)
                return null;

            if (!string.IsNullOrWhiteSpace(processConfigJson))
            {
                try
                {
                    processInstance.SetProcessConfig(processConfigJson);
                }
                catch (Exception ex)
                {
                    log.Warn($"恢复结果解析配置快照失败，将使用默认配置: {processTypeFullName}", ex);
                }
            }

            return processInstance;
        }

        private static string GetClassName(string processTypeFullName)
        {
            int separatorIndex = processTypeFullName.LastIndexOf('.');
            return processTypeFullName[(separatorIndex + 1)..];
        }
    }
}
