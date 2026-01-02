using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace ColorVision.Engine.Batch
{
    /// <summary>
    /// Interface for pre-processing operations that run before flow execution.
    /// Similar to IBatchProcess but executes before the flow starts.
    /// </summary>
    public interface IPreProcess
    {
        /// <summary>
        /// Executes the pre-processing logic before flow starts.
        /// </summary>
        /// <param name="ctx">The context containing flow information and configuration. Cannot be null.</param>
        /// <returns>true if pre-processing succeeded and flow should continue; false to abort flow execution.</returns>
        bool PreProcess(IPreProcessContext ctx);

        /// <summary>
        /// Gets the configuration object for this pre-processor.
        /// </summary>
        /// <returns>The configuration object, or null if no configuration is needed.</returns>
        object? GetConfig()
        {
            return null;
        }

        /// <summary>
        /// Sets the configuration from a JSON string.
        /// </summary>
        /// <param name="configJson">The JSON string representing the configuration.</param>
        void SetConfig(string configJson)
        {
            // Default implementation does nothing
        }

    }

    /// <summary>
    /// Base configuration class for pre-processors with enable/disable and template filtering support.
    /// </summary>
    public abstract class PreProcessConfigBase : ViewModelBase
    {
        /// <summary>
        /// Gets or sets whether this pre-processor is enabled for execution.
        /// </summary>
        [System.ComponentModel.DisplayName("启用")]
        [System.ComponentModel.Description("启用此预处理器")]
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled = false;

        /// <summary>
        /// Gets or sets the template names (comma-separated) this preprocessor applies to.
        /// Empty means applies to all templates.
        /// </summary>
        [System.ComponentModel.DisplayName("应用模板")]
        [System.ComponentModel.Description("逗号分隔的模板名称，留空表示应用于所有模板")]
        public string TemplateNames { get => _TemplateNames; set { _TemplateNames = value; OnPropertyChanged(); } }
        private string _TemplateNames = string.Empty;

        /// <summary>
        /// Checks if this preprocessor applies to the given template.
        /// </summary>
        public bool AppliesToTemplate(string templateName)
        {
            if (string.IsNullOrWhiteSpace(TemplateNames))
                return true; // Apply to all templates
            
            var templates = TemplateNames.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(t => t.Trim())
                                        .Where(t => !string.IsNullOrEmpty(t));
            
            return templates.Any(t => string.Equals(t, templateName, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Base class for pre-processors with typed configuration support.
    /// </summary>
    /// <typeparam name="T">The configuration type, must inherit from PreProcessConfigBase.</typeparam>
    public abstract class PreProcessBase<T> : IPreProcess where T : PreProcessConfigBase, new()
    {
        /// <summary>
        /// Gets or sets the configuration for this pre-processor.
        /// </summary>
        public T Config { get; set; } = new T();

        /// <summary>
        /// Gets the configuration object.
        /// </summary>
        public object? GetConfig() => Config;

        /// <summary>
        /// Sets the configuration from a JSON string.
        /// </summary>
        /// <param name="configJson">The JSON string representing the configuration.</param>
        public void SetConfig(string configJson)
        {
            if (!string.IsNullOrEmpty(configJson))
            {
                Config = JsonConvert.DeserializeObject<T>(configJson) ?? new T();
            }
        }
        /// <summary>
        /// Executes the pre-processing logic.
        /// </summary>
        public abstract bool PreProcess(IPreProcessContext ctx);
    }
}
