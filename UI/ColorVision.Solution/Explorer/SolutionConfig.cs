#pragma warning disable CS4014,CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Solution.Properties;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Solution.Explorer
{
    public enum SolutionProjectMode
    {
        AutoDiscover,
        Explicit,
    }

    public sealed class SolutionFolderDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "解决方案文件夹";
        public string? ParentId { get; set; }
    }

    public sealed class SolutionItemDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Path { get; set; } = string.Empty;
        public string? SolutionFolderId { get; set; }
    }

    /// <summary>
    /// 配置解决方案的模型类，支持MVVM绑定
    /// </summary>
    public class SolutionConfig : ViewModelBase
    {
        public int SchemaVersion { get; set; } = SolutionConfigStore.CurrentSchemaVersion;
        public string FilePath { get; set; }
        public string VirtualPath { get; set; }
        public string RootPath { get; set; } = string.Empty;
        public bool IsSetting { get; set; }
        public bool IsSetting1 { get; set; }
        public ObservableCollection<string> Paths { get; set; }

        /// <summary>
        /// 项目引用列表 - 存储相对于解决方案目录的项目路径
        /// 项目路径信息保存在 ColorVision 解决方案配置中
        /// </summary>
        public ObservableCollection<string> Projects { get; set; } = new ObservableCollection<string>();

        /// <summary>
        /// The project used by solution-level Run and Debug commands. The path
        /// is stored relative to the solution root whenever possible.
        /// </summary>
        public string StartupProject { get; set; } = string.Empty;

        /// <summary>
        /// The solution-wide configuration selected by build/run/debug. Project
        /// mappings may translate it to a differently named project profile.
        /// </summary>
        public string ActiveConfiguration { get; set; } = "Debug";

        /// <summary>
        /// The solution-wide platform selected together with
        /// <see cref="ActiveConfiguration"/>.
        /// </summary>
        public string ActivePlatform { get; set; } = SolutionConfigurationIdentity.DefaultPlatform;

        /// <summary>
        /// Project reference -> solution configuration -> project configuration.
        /// This mirrors the configuration mapping role of a Visual Studio solution.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> ProjectConfigurations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Virtual solution folders. These organize projects without changing
        /// their physical paths, matching the role of Visual Studio solution
        /// folders rather than ordinary file-system directories.
        /// </summary>
        public ObservableCollection<SolutionFolderDefinition> SolutionFolders { get; set; } = new();

        /// <summary>
        /// Project reference -> virtual solution-folder id.
        /// Projects not present in this map remain at the solution root.
        /// </summary>
        public Dictionary<string, string> ProjectSolutionFolders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Files referenced by the solution independently of any project.
        /// Removing one of these entries never deletes the physical file.
        /// </summary>
        public ObservableCollection<SolutionItemDefinition> SolutionItems { get; set; } = new();

        [JsonConverter(typeof(StringEnumConverter))]
        public SolutionProjectMode ProjectMode { get; set; } = SolutionProjectMode.AutoDiscover;

        [JsonExtensionData]
        public IDictionary<string, JToken>? ExtensionData { get; set; }
    }

    internal sealed record ProjectReferenceLoadResult(
        string Reference,
        ProjectDefinition? Project,
        string ResolvedPath,
        string ErrorMessage);

    internal sealed record ProjectRefreshResult(
        bool Succeeded,
        string ErrorMessage = "",
        bool Canceled = false);

    internal sealed record SolutionExplorerPreparation(
        SolutionConfigLoadResult LoadResult,
        DirectoryInfo RootDirectory,
        Dictionary<string, ProjectReferenceLoadResult> ProjectReferences);
}
