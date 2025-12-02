using ColorVision.Common.MVVM;
using Newtonsoft.Json;

namespace ColorVision.Engine.Batch
{
    public interface IBatchProcess
    {
        /// <summary>
        /// Processes the specified batch context and returns a value indicating whether the operation was successful.
        /// </summary>
        /// <param name="ctx">The batch context to process. Cannot be null.</param>
        /// <returns>true if the batch was processed successfully; otherwise, false.</returns>
        public bool Process(IBatchContext ctx);

        /// <summary>
        /// Gets the configuration object for this batch process.
        /// </summary>
        /// <returns>The configuration object, or null if no configuration is needed.</returns>
        public object? GetConfig()
        {
            return null;
        }

        /// <summary>
        /// Sets the configuration from a JSON string.
        /// </summary>
        /// <param name="configJson">The JSON string representing the configuration.</param>
        public void SetConfig(string configJson)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Creates a new instance of this batch process with the same type.
        /// </summary>
        /// <returns>A new instance of the batch process.</returns>
        public IBatchProcess CreateInstance()
        {
            return (IBatchProcess)System.Activator.CreateInstance(this.GetType());
        }
    }

    /// <summary>
    /// Base class for batch processes with typed configuration support.
    /// </summary>
    /// <typeparam name="T">The configuration type, must inherit from ViewModelBase.</typeparam>
    public abstract class BatchProcessBase<T> : IBatchProcess where T : ViewModelBase, new()
    {
        /// <summary>
        /// Gets or sets the configuration for this batch process.
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
        /// Creates a new instance of this batch process.
        /// </summary>
        public IBatchProcess CreateInstance()
        {
            return (IBatchProcess)System.Activator.CreateInstance(this.GetType());
        }

        /// <summary>
        /// Processes the batch context.
        /// </summary>
        public abstract bool Process(IBatchContext ctx);
    }
}
