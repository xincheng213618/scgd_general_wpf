using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Plugins
{
    public class PluginInfo : ViewModelBase
    {
        public PluginManifest Manifest { get; set; }

        [JsonIgnore]
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

        // These properties are loaded on-demand to reduce startup time and config size
        [JsonIgnore]
        public string README 
        { 
            get 
            {
                if (_README == null && !_READMELoaded)
                {
                    _READMELoaded = true;
                    _README = LoadReadme();
                }
                return _README ?? string.Empty;
            }
        }
        private string? _README;
        private bool _READMELoaded;

        [JsonIgnore]
        public string ChangeLog 
        { 
            get 
            {
                if (_ChangeLog == null && !_ChangeLogLoaded)
                {
                    _ChangeLogLoaded = true;
                    _ChangeLog = LoadChangelog();
                }
                return _ChangeLog ?? string.Empty;
            }
        }
        private string? _ChangeLog;
        private bool _ChangeLogLoaded;

        [JsonIgnore]
        public ImageSource? Icon 
        { 
            get 
            {
                if (_Icon == null && !_IconLoaded)
                {
                    _IconLoaded = true;
                    _Icon = LoadIcon();
                }
                return _Icon;
            }
        }
        private ImageSource? _Icon;
        private bool _IconLoaded;

        [JsonIgnore]
        public Assembly Assembly { get; set; }

        /// <summary>
        /// Gets the plugin directory path based on the plugin ID
        /// </summary>
        [JsonIgnore]
        public string PluginDirectory
        {
            get
            {
                if (Manifest?.Id != null)
                {
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", Manifest.Id);
                }
                return string.Empty;
            }
        }

        private string? LoadReadme()
        {
            try
            {
                string readmePath = Path.Combine(PluginDirectory, "readme.md");
                if (File.Exists(readmePath))
                {
                    return File.ReadAllText(readmePath);
                }
            }
            catch { /* ignore errors */ }
            return string.Empty;
        }

        private string? LoadChangelog()
        {
            try
            {
                string changelogPath = Path.Combine(PluginDirectory, "changelog.md");
                if (File.Exists(changelogPath))
                {
                    return File.ReadAllText(changelogPath);
                }
            }
            catch { /* ignore errors */ }
            return string.Empty;
        }

        private ImageSource? LoadIcon()
        {
            try
            {
                string iconPath = Path.Combine(PluginDirectory, "PackageIcon.png");
                if (File.Exists(iconPath))
                {
                    return new BitmapImage(new Uri(iconPath));
                }
            }
            catch { /* ignore errors */ }
            return null;
        }
    }
}