using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;

namespace ProjectARVRPro.Process
{
    public interface IProcess
    {
        public bool Execute(IProcessExecutionContext ctx);

        public void Render(IProcessExecutionContext ctx);

        public string GenText(IProcessExecutionContext ctx);

        /// <summary>
        /// Gets the recipe configuration for this process.
        /// </summary>
        /// <returns>The recipe configuration, or null if no recipe config is available.</returns>
        public IRecipeConfig GetRecipeConfig()
        {
            return null;
        }

        /// <summary>
        /// Gets the fix configuration for this process.
        /// </summary>
        /// <returns>The fix configuration, or null if no fix config is available.</returns>
        public IFixConfig GetFixConfig()
        {
            return null;
        }

        /// <summary>
        /// Gets the process-specific configuration for this process.
        /// </summary>
        /// <returns>The process configuration object, or null if no process config is available.</returns>
        public object GetProcessConfig()
        {
            return null;
        }

        /// <summary>
        /// Sets the process configuration from a JSON string.
        /// </summary>
        /// <param name="configJson">The JSON string representing the configuration.</param>
        public void SetProcessConfig(string configJson)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Creates a new instance of this process with the same type.
        /// </summary>
        /// <returns>A new instance of the process.</returns>
        public IProcess CreateInstance()
        {
            try
            {
                return (IProcess)System.Activator.CreateInstance(this.GetType());
            }
            catch
            {
                return this;
            }
        }
    }

    /// <summary>
    /// Base class for processes with typed configuration support.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type, must inherit from ViewModelBase.</typeparam>
    public abstract class ProcessBase<TConfig> : IProcess where TConfig : ViewModelBase, new()
    {
        /// <summary>
        /// Gets or sets the configuration for this process.
        /// </summary>
        public TConfig Config { get; set; } = new TConfig();

        /// <summary>
        /// Gets the process configuration object.
        /// </summary>
        public object GetProcessConfig() => Config;

        /// <summary>
        /// Sets the process configuration from a JSON string.
        /// </summary>
        /// <param name="configJson">The JSON string representing the configuration.</param>
        public void SetProcessConfig(string configJson)
        {
            if (!string.IsNullOrEmpty(configJson))
            {
                Config = JsonConvert.DeserializeObject<TConfig>(configJson) ?? new TConfig();
            }
        }

        /// <summary>
        /// Creates a new instance of this process.
        /// </summary>
        public IProcess CreateInstance()
        {
            try
            {
                return (IProcess)System.Activator.CreateInstance(this.GetType());
            }
            catch
            {
                return this;
            }
        }

        public abstract bool Execute(IProcessExecutionContext ctx);
        public abstract void Render(IProcessExecutionContext ctx);
        public abstract string GenText(IProcessExecutionContext ctx);

        public virtual IRecipeConfig GetRecipeConfig() => null;
        public virtual IFixConfig GetFixConfig() => null;
    }
}
