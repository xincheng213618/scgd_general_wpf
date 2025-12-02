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
        /// <returns>The process configuration, or null if no process config is available.</returns>
        public IProcessConfig GetProcessConfig()
        {
            return null;
        }
    }
}
