#pragma warning disable CS8603
using ColorVision.Common.MVVM;
using Newtonsoft.Json;

namespace ColorVision.Engine.FlowProcessing.PostProcess
{
    public interface IPostProcessor
    {
        /// <summary>
        /// Processes the specified completed flow batch.
        /// </summary>
        bool Process(PostProcessContext ctx);

        /// <summary>
        /// Gets the configuration object for this post-processor.
        /// </summary>
        object? GetConfig()
        {
            return null;
        }

        /// <summary>
        /// Sets the configuration from a JSON string.
        /// </summary>
        void SetConfig(string configJson)
        {
        }

        /// <summary>
        /// Creates a new instance of this post-processor with the same type.
        /// </summary>
        IPostProcessor CreateInstance()
        {
            try
            {
                return (IPostProcessor)System.Activator.CreateInstance(GetType());
            }
            catch
            {
                return this;
            }
        }
    }

    /// <summary>
    /// Base class for post-processors with typed configuration support.
    /// </summary>
    public abstract class PostProcessorBase<T> : IPostProcessor where T : ViewModelBase, new()
    {
        public T Config { get; set; } = new T();

        public object? GetConfig() => Config;

        public void SetConfig(string configJson)
        {
            if (!string.IsNullOrEmpty(configJson))
            {
                Config = JsonConvert.DeserializeObject<T>(configJson) ?? new T();
            }
        }

        public IPostProcessor CreateInstance()
        {
            try
            {
                return (IPostProcessor)System.Activator.CreateInstance(GetType());
            }
            catch
            {
                return this;
            }
        }

        public abstract bool Process(PostProcessContext ctx);
    }
}
