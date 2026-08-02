using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.PostProcess
{
    public sealed class PostProcessTypeOption
    {
        public required IPostProcessor Process { get; init; }
        public required string Category { get; init; }
        public required string DisplayName { get; init; }
        public required string Description { get; init; }
        public required string ConfigurationSummary { get; init; }
        public required int Order { get; init; }

        public string TypeName => Process.GetType().Name;
        public string FullTypeName => Process.GetType().FullName ?? TypeName;
        public string AssemblyName => Process.GetType().Assembly.GetName().Name ?? string.Empty;
    }

    public static class PostProcessTypeCatalog
    {
        public const string ArvrCategory = "ARVR";
        public const string IvlCategory = "IVL";
        public const string DataExportCategory = "数据导出";
        public const string DeviceControlCategory = "设备控制";
        public const string MaintenanceCategory = "系统维护";
        public const string ProjectPluginCategory = "项目插件";
        public const string GeneralCategory = "通用";

        public static IReadOnlyList<PostProcessTypeOption> CreateOptions(IEnumerable<IPostProcessor> processes)
        {
            ArgumentNullException.ThrowIfNull(processes);

            return processes
                .GroupBy(process => process.GetType().FullName, StringComparer.Ordinal)
                .Select(group => CreateOption(group.First()))
                .OrderBy(option => GetCategoryOrder(option.Category))
                .ThenBy(option => option.Category, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(option => option.Order)
                .ThenBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static PostProcessTypeOption CreateOption(IPostProcessor process)
        {
            PostProcessMetadata metadata = PostProcessMetadata.FromProcess(process);
            return new PostProcessTypeOption
            {
                Process = process,
                Category = ResolveCategory(process, metadata),
                DisplayName = metadata.DisplayName,
                Description = string.IsNullOrWhiteSpace(metadata.Description)
                    ? $"流程完成后执行 {metadata.DisplayName}。"
                    : metadata.Description,
                ConfigurationSummary = process.GetConfig() == null ? "无需额外配置" : "支持处理配置",
                Order = metadata.Order
            };
        }

        private static string ResolveCategory(IPostProcessor process, PostProcessMetadata metadata)
        {
            if (!string.IsNullOrWhiteSpace(metadata.Category))
                return metadata.Category;

            Type processType = process.GetType();
            string typeNamespace = processType.Namespace ?? string.Empty;
            if (typeNamespace.Contains(".IVL", StringComparison.OrdinalIgnoreCase))
                return IvlCategory;
            if (typeNamespace.Contains(".Smu", StringComparison.OrdinalIgnoreCase))
                return DeviceControlCategory;
            if (typeNamespace.Contains(".Poi", StringComparison.OrdinalIgnoreCase))
                return DataExportCategory;
            if (processType.Name.Contains("Cleanup", StringComparison.OrdinalIgnoreCase))
                return MaintenanceCategory;
            if (processType.Assembly != typeof(PostProcessTypeCatalog).Assembly)
                return ProjectPluginCategory;

            return GeneralCategory;
        }

        private static int GetCategoryOrder(string category) => category switch
        {
            ArvrCategory => 0,
            IvlCategory => 1,
            DataExportCategory => 2,
            DeviceControlCategory => 3,
            MaintenanceCategory => 4,
            ProjectPluginCategory => 5,
            GeneralCategory => 6,
            _ => 7
        };
    }
}
