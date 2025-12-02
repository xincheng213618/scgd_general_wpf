using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
using System;
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

        public IProcess Process 
        { 
            get => _Process; 
            set 
            { 
                _Process = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(ProcessTypeName)); 
                OnPropertyChanged(nameof(HasConfig));
                OnPropertyChanged(nameof(RecipeConfigTypeName));
                OnPropertyChanged(nameof(FixConfigTypeName));
                OnPropertyChanged(nameof(HasRecipeConfig));
                OnPropertyChanged(nameof(HasFixConfig));
            } 
        }
        private IProcess _Process;

        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled = true;

        public string ProcessTypeName => Process?.GetType().Name ?? string.Empty;
        public string ProcessTypeFullName => Process?.GetType().FullName ?? string.Empty;

        /// <summary>
        /// Gets the corresponding recipe config type name based on the Process type.
        /// Naming convention: {Prefix}Process -> {Prefix}RecipeConfig
        /// </summary>
        [JsonIgnore]
        public string RecipeConfigTypeName
        {
            get
            {
                if (Process == null) return string.Empty;
                string processName = Process.GetType().Name;
                // Handle special case: White255Process -> W255RecipeConfig
                if (processName == "White255Process")
                    return "W255RecipeConfig";
                // Standard pattern: {Prefix}Process -> {Prefix}RecipeConfig
                if (processName.EndsWith("Process", StringComparison.Ordinal))
                {
                    string prefix = processName.Substring(0, processName.Length - "Process".Length);
                    return prefix + "RecipeConfig";
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the corresponding fix config type name based on the Process type.
        /// Naming convention: {Prefix}Process -> {Prefix}FixConfig
        /// </summary>
        [JsonIgnore]
        public string FixConfigTypeName
        {
            get
            {
                if (Process == null) return string.Empty;
                string processName = Process.GetType().Name;
                // Handle special case: White255Process -> W255FixConfig
                if (processName == "White255Process")
                    return "W255FixConfig";
                // Standard pattern: {Prefix}Process -> {Prefix}FixConfig
                if (processName.EndsWith("Process", StringComparison.Ordinal))
                {
                    string prefix = processName.Substring(0, processName.Length - "Process".Length);
                    return prefix + "FixConfig";
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this process has a configurable config.
        /// </summary>
        [JsonIgnore]
        public bool HasConfig => Process?.GetType().GetMethod("GetConfig") != null;

        /// <summary>
        /// Gets a value indicating whether a recipe config exists for this process.
        /// </summary>
        [JsonIgnore]
        public bool HasRecipeConfig
        {
            get
            {
                if (string.IsNullOrEmpty(RecipeConfigTypeName)) return false;
                var recipeManager = RecipeManager.GetInstance();
                return recipeManager.RecipeConfig.Configs.Keys.Any(t => t.Name == RecipeConfigTypeName);
            }
        }

        /// <summary>
        /// Gets a value indicating whether a fix config exists for this process.
        /// </summary>
        [JsonIgnore]
        public bool HasFixConfig
        {
            get
            {
                if (string.IsNullOrEmpty(FixConfigTypeName)) return false;
                var fixManager = FixManager.GetInstance();
                return fixManager.FixConfig.Configs.Keys.Any(t => t.Name == FixConfigTypeName);
            }
        }

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
        /// Opens the PropertyEditorWindow to edit the corresponding recipe configuration.
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
        /// Opens the PropertyEditorWindow to edit the corresponding fix configuration.
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
    }
}
