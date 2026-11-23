using log4net;
using Newtonsoft.Json;
using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Process.MTF.MTFHV058;
using ProjectARVRPro.Process.POI;
using ProjectARVRPro.Process.W51;
using ProjectARVRPro.Process.W255;

namespace ProjectARVRPro.Process
{
    /// <summary>
    /// Captures and restores the process required to replay a persisted result.
    /// </summary>
    public static class ResultProcessResolver
    {
        private const int MaxReusableCsvSnapshots = 128;
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
            => Resolve(result, processTemplates, legacyProcessMetas, csvSnapshots: null);

        internal static IProcess? Resolve(
            ProjectARVRReuslt result,
            IEnumerable<IProcess> processTemplates,
            IEnumerable<ProcessMeta> legacyProcessMetas,
            Dictionary<(string ProcessType, string? ConfigJson), IProcess>? csvSnapshots)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(processTemplates);
            ArgumentNullException.ThrowIfNull(legacyProcessMetas);

            if (!string.IsNullOrWhiteSpace(result.ProcessTypeFullName))
            {
                IProcess? process = CreateProcess(result.ProcessTypeFullName, result.ProcessConfigJson, processTemplates, csvSnapshots);
                if (process != null)
                    return process;

                log.Warn($"历史结果解析器不可用: {result.ProcessTypeFullName}; 尝试按模板 {result.Model} 兼容解析");
            }

            return FindMapping(result.Model, legacyProcessMetas)?.Process;
        }

        /// <summary>
        /// Resolves the process used to draw a persisted result. When the active configuration
        /// still maps the same flow to the same process type, presentation-only settings should
        /// follow that current process instead of the immutable execution snapshot.
        /// </summary>
        public static IProcess? ResolveForRender(
            ProjectARVRReuslt result,
            IEnumerable<IProcess> processTemplates,
            IEnumerable<ProcessMeta> currentProcessMetas)
        {
            ArgumentNullException.ThrowIfNull(currentProcessMetas);

            ProcessMeta[] currentMappings = currentProcessMetas as ProcessMeta[] ?? currentProcessMetas.ToArray();
            IProcess? snapshotProcess = Resolve(result, processTemplates, currentMappings);
            IProcess? currentProcess = FindMapping(result.Model, currentMappings)?.Process;
            return snapshotProcess != null
                && currentProcess != null
                && currentProcess.GetType() == snapshotProcess.GetType()
                    ? currentProcess
                    : snapshotProcess;
        }

        public static ProcessMeta? FindMapping(
            string flowTemplate,
            IEnumerable<ProcessMeta> primaryMappings,
            IEnumerable<ProcessMeta>? fallbackMappings = null)
        {
            ArgumentNullException.ThrowIfNull(primaryMappings);

            ProcessMeta? mapping = primaryMappings.FirstOrDefault(meta =>
                meta.Process != null
                && string.Equals(meta.FlowTemplate, flowTemplate, StringComparison.OrdinalIgnoreCase));
            if (mapping != null || fallbackMappings == null)
                return mapping;

            return fallbackMappings.FirstOrDefault(meta =>
                meta.Process != null
                && string.Equals(meta.FlowTemplate, flowTemplate, StringComparison.OrdinalIgnoreCase));
        }

        private static IProcess? CreateProcess(
            string processTypeFullName,
            string? processConfigJson,
            IEnumerable<IProcess> processTemplates,
            Dictionary<(string ProcessType, string? ConfigJson), IProcess>? csvSnapshots)
        {
            if (csvSnapshots != null && JsonConvert.DefaultSettings != null)
                csvSnapshots = null;

            var key = (processTypeFullName, processConfigJson);
            if (csvSnapshots?.TryGetValue(key, out IProcess? restored) == true)
                return restored;

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
                    return processInstance;
                }

                // Reuse only private, fully restored instances with reviewed CSV extractors.
                Type type = processInstance.GetType();
                if (csvSnapshots is { Count: < MaxReusableCsvSnapshots } && !ReferenceEquals(processInstance, template)
                    && type == template?.GetType() && type.FullName == processTypeFullName
                    && IsReadOnlyCsvExtractor(type))
                    csvSnapshots.Add(key, processInstance);
            }

            return processInstance;
        }

        // These read-only overrides all use the shared ProcessBase CSV reader.
        internal static bool IsReadOnlyCsvExtractor(Type type)
            => type == typeof(PoiDynamicProcess) || type == typeof(White51Process) || type == typeof(White255Process)
                || type == typeof(MTFHV058Process) || type == typeof(ChessboardProcess);

        private static string GetClassName(string processTypeFullName)
        {
            int separatorIndex = processTypeFullName.LastIndexOf('.');
            return processTypeFullName[(separatorIndex + 1)..];
        }
    }
}
