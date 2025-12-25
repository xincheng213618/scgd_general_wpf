using ColorVision.Common.MVVM;
using Newtonsoft.Json;

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
    /// Base class for pre-processors with typed configuration support.
    /// </summary>
    /// <typeparam name="T">The configuration type, must inherit from ViewModelBase.</typeparam>
    public abstract class PreProcessBase<T> : IPreProcess where T : ViewModelBase, new()
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
