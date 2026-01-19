using Quartz;

namespace ColorVision.Scheduler
{
    public interface IJobConfig : IJob
    {
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
}
