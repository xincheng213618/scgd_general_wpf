using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Reflection;
using System.Windows.Media;

namespace ColorVision.UI.Plugins
{
    public class PluginInfo : ViewModelBase
    {
        public PluginManifest Manifest { get; set; }

        public DepsJson DepsJson { get; set; }
        public bool Enabled { get; set; } = true;
        public string? Name { get; set; }

        public string Description { get; set; }

        public Version? AssemblyVersion { get; set; }
        public DateTime? AssemblyBuildDate { get; set; }
        public string? AssemblyName { get; set; }
        public string? AssemblyPath { get; set; }
        public string? AssemblyCulture { get; set; }
        public string? AssemblyPublicKeyToken { get; set; }

        public string README { get; set; } = string.Empty;

        public string ChangeLog { get; set; } = string.Empty;

        [JsonIgnore]
        public ImageSource? Icon { get; set; }
        [JsonIgnore]
        public Assembly Assembly { get; set; }
    }
}