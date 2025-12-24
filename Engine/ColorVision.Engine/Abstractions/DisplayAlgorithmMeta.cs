using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine
{
    public class DisplayAlgorithmMeta : ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        /// <summary>
        /// Gets or sets user-defined tag/label for this display algorithm.
        /// </summary>
        [DisplayName("标签")]
        [Description("用户自定义标签信息")]
        public string Tag { get => _Tag; set { _Tag = value; OnPropertyChanged(); } }
        private string _Tag;

        /// <summary>
        /// Gets or sets the JSON representation of the display algorithm configuration.
        /// </summary>
        public string ConfigJson { get => _ConfigJson; set { _ConfigJson = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasConfig)); } }
        private string _ConfigJson;

        public IDisplayAlgorithm DisplayAlgorithm 
        { 
            get => _DisplayAlgorithm; 
            set 
            { 
                _DisplayAlgorithm = value; 
                _cachedMetadata = null; // Clear cache when algorithm changes
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(AlgorithmTypeName)); 
                OnPropertyChanged(nameof(AlgorithmDisplayName));
                OnPropertyChanged(nameof(AlgorithmGroup));
                OnPropertyChanged(nameof(Metadata));
                OnPropertyChanged(nameof(HasConfig));
            } 
        }
        private IDisplayAlgorithm _DisplayAlgorithm;

        private DisplayAlgorithmMetadata? _cachedMetadata;

        public string AlgorithmTypeName => DisplayAlgorithm?.GetType().Name ?? string.Empty;
        public string AlgorithmTypeFullName => DisplayAlgorithm?.GetType().FullName ?? string.Empty;

        /// <summary>
        /// Gets the display order of this algorithm in the list.
        /// </summary>
        [JsonIgnore]
        public string Order
        {
            get
            {
                var manager = DisplayAlgorithmManager.GetInstance();
                if (manager == null)
                    return string.Empty;

                int index = manager.AlgorithmMetas.IndexOf(this);
                return index >= 0 ? (index + 1).ToString() : string.Empty;
            }
        }
        
        /// <summary>
        /// Public method to raise property changed event for Order property.
        /// </summary>
        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        /// <summary>
        /// Gets a value indicating whether this display algorithm has a configurable config.
        /// </summary>
        [JsonIgnore]
        public bool HasConfig => DisplayAlgorithm != null;

        /// <summary>
        /// Command to edit the display algorithm configuration.
        /// </summary>
        [JsonIgnore]
        public RelayCommand EditConfigCommand => _EditConfigCommand ??= new RelayCommand(
            a => EditConfig(),
            a => HasConfig
        );
        private RelayCommand _EditConfigCommand;

        /// <summary>
        /// Opens the PropertyEditorWindow to edit the display algorithm configuration.
        /// </summary>
        private void EditConfig()
        {
            if (DisplayAlgorithm == null) return;

            var editor = new PropertyEditorWindow(DisplayAlgorithm) 
            { 
                Owner = Application.Current.GetActiveWindow(), 
                WindowStartupLocation = WindowStartupLocation.CenterOwner 
            };
            
            editor.ShowDialog();
            
            // Save configuration after editing
            ConfigJson = JsonConvert.SerializeObject(DisplayAlgorithm);
        }

        /// <summary>
        /// Applies the stored config JSON to the display algorithm.
        /// </summary>
        public void ApplyConfig()
        {
            if (DisplayAlgorithm != null && !string.IsNullOrEmpty(ConfigJson))
            {
                try
                {
                    JsonConvert.PopulateObject(ConfigJson, DisplayAlgorithm);
                }
                catch
                {
                    // If deserialization fails, ignore
                }
            }
        }

        /// <summary>
        /// Gets the metadata for the display algorithm.
        /// </summary>
        [JsonIgnore]
        public DisplayAlgorithmMetadata Metadata
        {
            get
            {
                if (_cachedMetadata == null && DisplayAlgorithm != null)
                {
                    _cachedMetadata = DisplayAlgorithmMetadata.FromAlgorithm(DisplayAlgorithm);
                }
                return _cachedMetadata ?? new DisplayAlgorithmMetadata();
            }
        }

        /// <summary>
        /// Gets the display name from metadata (falls back to type name if no metadata).
        /// </summary>
        [JsonIgnore]
        public string AlgorithmDisplayName => Metadata.DisplayName;

        /// <summary>
        /// Gets the group from metadata.
        /// </summary>
        [JsonIgnore]
        public string AlgorithmGroup => Metadata.Group;
    }
}
