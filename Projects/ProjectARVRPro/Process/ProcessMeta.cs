using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Windows;

namespace ProjectARVRPro.Process
{
    public class ProcessMeta : ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public string FlowTemplate { get => _FlowTemplate; set { _FlowTemplate = value; OnPropertyChanged(); } }
        private string _FlowTemplate;

        /// <summary>
        /// Gets or sets the JSON representation of the process configuration.
        /// </summary>
        public string ConfigJson { get => _ConfigJson; set { _ConfigJson = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProcessConfig)); } }
        private string _ConfigJson;

        public IProcess Process 
        { 
            get => _Process; 
            set 
            { 
                _Process = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(ProcessTypeName)); 
                OnPropertyChanged(nameof(RecipeConfigTypeName));
                OnPropertyChanged(nameof(FixConfigTypeName));
                OnPropertyChanged(nameof(ProcessConfigTypeName));
                OnPropertyChanged(nameof(HasRecipeConfig));
                OnPropertyChanged(nameof(HasFixConfig));
                OnPropertyChanged(nameof(HasProcessConfig));
            } 
        }
        private IProcess _Process;

        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled = true;

        public string ProcessTypeName => Process?.GetType().Name ?? string.Empty;
        public string ProcessTypeFullName => Process?.GetType().FullName ?? string.Empty;

        /// <summary>
        /// Gets the recipe config type name from the Process.
        /// </summary>
        [JsonIgnore]
        public string RecipeConfigTypeName => Process?.GetRecipeConfig()?.GetType().Name ?? string.Empty;

        /// <summary>
        /// Gets the fix config type name from the Process.
        /// </summary>
        [JsonIgnore]
        public string FixConfigTypeName => Process?.GetFixConfig()?.GetType().Name ?? string.Empty;

        /// <summary>
        /// Gets the process config type name from the Process.
        /// </summary>
        [JsonIgnore]
        public string ProcessConfigTypeName => Process?.GetProcessConfig()?.GetType().Name ?? string.Empty;

        /// <summary>
        /// Gets a value indicating whether a recipe config exists for this process.
        /// </summary>
        [JsonIgnore]
        public bool HasRecipeConfig => Process?.GetRecipeConfig() != null;

        /// <summary>
        /// Gets a value indicating whether a fix config exists for this process.
        /// </summary>
        [JsonIgnore]
        public bool HasFixConfig => Process?.GetFixConfig() != null;

        /// <summary>
        /// Gets a value indicating whether a process config exists for this process.
        /// </summary>
        [JsonIgnore]
        public bool HasProcessConfig => Process?.GetProcessConfig() != null;

        /// <summary>
        /// Command to edit the recipe configuration.
        /// </summary>
        [JsonIgnore]
        public RelayCommand EditRecipeConfigCommand => _EditRecipeConfigCommand ??= new RelayCommand(
            a => EditRecipeConfig(),
            a => HasRecipeConfig
        );
        private RelayCommand _EditRecipeConfigCommand;

        /// <summary>
        /// Command to edit the fix configuration.
        /// </summary>
        [JsonIgnore]
        public RelayCommand EditFixConfigCommand => _EditFixConfigCommand ??= new RelayCommand(
            a => EditFixConfig(),
            a => HasFixConfig
        );
        private RelayCommand _EditFixConfigCommand;

        /// <summary>
        /// Command to edit the process configuration.
        /// </summary>
        [JsonIgnore]
        public RelayCommand EditProcessConfigCommand => _EditProcessConfigCommand ??= new RelayCommand(
            a => EditProcessConfig(),
            a => HasProcessConfig
        );
        private RelayCommand _EditProcessConfigCommand;

        /// <summary>
        /// Opens the PropertyEditorWindow to edit the recipe configuration.
        /// </summary>
        private void EditRecipeConfig()
        {
            var recipeConfig = Process?.GetRecipeConfig();
            if (recipeConfig == null) return;

            var editor = new PropertyEditorWindow(recipeConfig)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            editor.ShowDialog();
            RecipeManager.GetInstance().Save();
        }

        /// <summary>
        /// Opens the PropertyEditorWindow to edit the fix configuration.
        /// </summary>
        private void EditFixConfig()
        {
            var fixConfig = Process?.GetFixConfig();
            if (fixConfig == null) return;

            var editor = new PropertyEditorWindow(fixConfig)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            editor.ShowDialog();
            FixManager.GetInstance().Save();
        }

        /// <summary>
        /// Opens the PropertyEditorWindow to edit the process configuration.
        /// </summary>
        private void EditProcessConfig()
        {
            if (Process == null) return;

            var config = Process.GetProcessConfig();
            if (config == null) return;

            var editor = new PropertyEditorWindow(config)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            editor.ShowDialog();

            // Save the config as JSON after editing
            ConfigJson = JsonConvert.SerializeObject(config);
        }

        /// <summary>
        /// Applies the stored config JSON to the process.
        /// </summary>
        public void ApplyConfig()
        {
            if (Process != null && !string.IsNullOrEmpty(ConfigJson))
            {
                Process.SetProcessConfig(ConfigJson);
            }
        }
    }
}
