using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
using System.Linq;
using System.Windows;

namespace ProjectARVRPro.Process
{
    public class ProcessMeta : ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public string FlowTemplate { get => _FlowTemplate; set { _FlowTemplate = value; OnPropertyChanged(); } }
        private string _FlowTemplate;

        public IProcess Process { get => _Process; set { _Process = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProcessTypeName)); OnPropertyChanged(nameof(HasConfig)); } }
        private IProcess _Process;

        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled = true;

        public string ProcessTypeName => Process?.GetType().Name ?? string.Empty;
        public string ProcessTypeFullName => Process?.GetType().FullName ?? string.Empty;

        /// <summary>
        /// Gets or sets the JSON representation of the process configuration.
        /// </summary>
        public string ConfigJson { get => _ConfigJson; set { _ConfigJson = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasConfig)); } }
        private string _ConfigJson;

        /// <summary>
        /// Gets or sets the selected recipe config type name.
        /// </summary>
        public string RecipeConfigTypeName { get => _RecipeConfigTypeName; set { _RecipeConfigTypeName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasRecipeConfig)); } }
        private string _RecipeConfigTypeName;

        /// <summary>
        /// Gets or sets the selected fix config type name.
        /// </summary>
        public string FixConfigTypeName { get => _FixConfigTypeName; set { _FixConfigTypeName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFixConfig)); } }
        private string _FixConfigTypeName;

        /// <summary>
        /// Gets a value indicating whether this process has a configurable config.
        /// </summary>
        [JsonIgnore]
        public bool HasConfig => Process?.GetType().GetMethod("GetConfig") != null;

        /// <summary>
        /// Gets a value indicating whether a recipe config is selected.
        /// </summary>
        [JsonIgnore]
        public bool HasRecipeConfig => !string.IsNullOrEmpty(RecipeConfigTypeName);

        /// <summary>
        /// Gets a value indicating whether a fix config is selected.
        /// </summary>
        [JsonIgnore]
        public bool HasFixConfig => !string.IsNullOrEmpty(FixConfigTypeName);

        /// <summary>
        /// Command to edit the process configuration.
        /// </summary>
        [JsonIgnore]
        public RelayCommand EditConfigCommand => _EditConfigCommand ??= new RelayCommand(
            a => EditConfig(),
            a => HasConfig
        );
        private RelayCommand _EditConfigCommand;

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
        /// Opens the PropertyEditorWindow to edit the process configuration.
        /// </summary>
        private void EditConfig()
        {
            if (Process == null) return;

            var configMethod = Process.GetType().GetMethod("GetConfig");
            if (configMethod == null) return;

            var config = configMethod.Invoke(Process, null);
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
        /// Opens the PropertyEditorWindow to edit the selected recipe configuration.
        /// </summary>
        private void EditRecipeConfig()
        {
            if (string.IsNullOrEmpty(RecipeConfigTypeName)) return;

            var recipeManager = RecipeManager.GetInstance();
            var recipeType = recipeManager.RecipeConfig.Configs.Keys.FirstOrDefault(t => t.Name == RecipeConfigTypeName);
            if (recipeType == null) return;

            var recipeConfig = recipeManager.RecipeConfig.Configs[recipeType];
            if (recipeConfig == null) return;

            var editor = new PropertyEditorWindow(recipeConfig)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            editor.ShowDialog();
            recipeManager.Save();
        }

        /// <summary>
        /// Opens the PropertyEditorWindow to edit the selected fix configuration.
        /// </summary>
        private void EditFixConfig()
        {
            if (string.IsNullOrEmpty(FixConfigTypeName)) return;

            var fixManager = FixManager.GetInstance();
            var fixType = fixManager.FixConfig.Configs.Keys.FirstOrDefault(t => t.Name == FixConfigTypeName);
            if (fixType == null) return;

            var fixConfig = fixManager.FixConfig.Configs[fixType];
            if (fixConfig == null) return;

            var editor = new PropertyEditorWindow(fixConfig)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            editor.ShowDialog();
            fixManager.Save();
        }

        /// <summary>
        /// Applies the stored config JSON to the process.
        /// </summary>
        public void ApplyConfig()
        {
            if (Process != null && !string.IsNullOrEmpty(ConfigJson))
            {
                var setConfigMethod = Process.GetType().GetMethod("SetConfig");
                setConfigMethod?.Invoke(Process, new object[] { ConfigJson });
            }
        }
    }
}
