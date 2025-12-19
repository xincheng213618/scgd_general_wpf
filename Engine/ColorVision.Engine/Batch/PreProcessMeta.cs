using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Batch
{
    public class PreProcessMeta : ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public string TemplateName { get => _TemplateName; set { _TemplateName = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExecutionOrder)); } }
        private string _TemplateName;

        /// <summary>
        /// Gets or sets user-defined tag/label for this pre-processor.
        /// </summary>
        [DisplayName("标签")]
        [Description("用户自定义标签信息")]
        public string Tag { get => _Tag; set { _Tag = value; OnPropertyChanged(); } }
        private string _Tag;

        /// <summary>
        /// Gets or sets the JSON representation of the pre-processor configuration.
        /// </summary>
        public string ConfigJson { get => _ConfigJson; set { _ConfigJson = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasConfig)); } }
        private string _ConfigJson;

        public IPreProcess PreProcess 
        { 
            get => _PreProcess; 
            set 
            { 
                _PreProcess = value; 
                _cachedMetadata = null; // Clear cache when process changes
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(ProcessTypeName)); 
                OnPropertyChanged(nameof(ProcessDisplayName));
                OnPropertyChanged(nameof(ProcessDescription));
                OnPropertyChanged(nameof(ProcessCategory));
                OnPropertyChanged(nameof(Metadata));
                OnPropertyChanged(nameof(HasConfig));
            } 
        }
        private IPreProcess _PreProcess;

        private PreProcessMetadata? _cachedMetadata;

        public string ProcessTypeName => PreProcess?.GetType().Name ?? string.Empty;
        public string ProcessTypeFullName => PreProcess?.GetType().FullName ?? string.Empty;

        /// <summary>
        /// Gets a value indicating whether this pre-processor has a configurable config.
        /// </summary>
        [JsonIgnore]
        public bool HasConfig => PreProcess?.GetConfig() != null;

        /// <summary>
        /// Command to edit the pre-processor configuration.
        /// </summary>
        [JsonIgnore]
        public RelayCommand EditConfigCommand => _EditConfigCommand ??= new RelayCommand(
            a => EditConfig(),
            a => HasConfig
        );
        private RelayCommand _EditConfigCommand;

        /// <summary>
        /// Opens the PropertyEditorWindow to edit the pre-processor configuration.
        /// </summary>
        private void EditConfig()
        {
            if (PreProcess == null) return;
            
            var config = PreProcess.GetConfig();
            if (config == null) return;

            var editor = new PropertyEditorWindow(config) 
            { 
                Owner = Application.Current.GetActiveWindow(), 
                WindowStartupLocation = WindowStartupLocation.CenterOwner 
            };
            
            editor.ShowDialog();
            
            // PropertyEditorWindow edits config in-place, so save after dialog closes
            ConfigJson = JsonConvert.SerializeObject(config);
        }

        /// <summary>
        /// Applies the stored config JSON to the pre-processor.
        /// </summary>
        public void ApplyConfig()
        {
            if (PreProcess != null && !string.IsNullOrEmpty(ConfigJson))
            {
                PreProcess.SetConfig(ConfigJson);
            }
        }

        /// <summary>
        /// Gets the execution order of this process within its flow template.
        /// This shows the order in which processes with the same TemplateName will be executed.
        /// </summary>
        [JsonIgnore]
        public string ExecutionOrder
        {
            get
            {
                var manager = PreProcessManager.GetInstance();
                if (manager == null || string.IsNullOrEmpty(TemplateName))
                    return string.Empty;

                var sameTemplateItems = manager.ProcessMetas
                    .Where(m => string.Equals(m.TemplateName, TemplateName, System.StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (sameTemplateItems.Count <= 1)
                    return string.Empty;

                int index = sameTemplateItems.IndexOf(this);
                return index >= 0 ? $"{index + 1}/{sameTemplateItems.Count}" : string.Empty;
            }
        }

        /// <summary>
        /// Notifies that the execution order may have changed.
        /// </summary>
        public void NotifyExecutionOrderChanged()
        {
            OnPropertyChanged(nameof(ExecutionOrder));
        }

        /// <summary>
        /// Gets the metadata for the pre-processor.
        /// </summary>
        [JsonIgnore]
        public PreProcessMetadata Metadata
        {
            get
            {
                if (_cachedMetadata == null && PreProcess != null)
                {
                    _cachedMetadata = PreProcessMetadata.FromProcess(PreProcess);
                }
                return _cachedMetadata ?? new PreProcessMetadata();
            }
        }

        /// <summary>
        /// Gets the display name from metadata (falls back to type name if no metadata).
        /// </summary>
        [JsonIgnore]
        public string ProcessDisplayName => Metadata.DisplayName;

        /// <summary>
        /// Gets the description from metadata.
        /// </summary>
        [JsonIgnore]
        public string ProcessDescription => Metadata.Description;

        /// <summary>
        /// Gets the category from metadata.
        /// </summary>
        [JsonIgnore]
        public string ProcessCategory => Metadata.Category;
    }
}
