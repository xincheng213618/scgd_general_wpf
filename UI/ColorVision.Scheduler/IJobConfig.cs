using ColorVision.Common.MVVM;
using System;

namespace ColorVision.Scheduler
{
    /// <summary>
    /// Interface for job configuration objects
    /// Implementations should inherit from ViewModelBase and use PropertyEditor attributes
    /// </summary>
    public interface IJobConfig
    {
    }

    /// <summary>
    /// Base class for job configurations
    /// </summary>
    public class JobConfigBase : ViewModelBase, IJobConfig
    {
    }

    /// <summary>
    /// Interface for jobs that support configuration
    /// </summary>
    public interface IConfigurableJob
    {
        /// <summary>
        /// Returns the type of configuration this job uses
        /// </summary>
        Type ConfigType { get; }

        /// <summary>
        /// Creates a default instance of the configuration
        /// </summary>
        IJobConfig CreateDefaultConfig();
    }
}
