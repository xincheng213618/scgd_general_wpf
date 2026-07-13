#pragma warning disable CS8603
using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;

namespace ProjectARVRPro.Process
{
    public interface IProcess
    {
        public Task<bool> Execute(IProcessExecutionContext ctx);

        public Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            return Task.FromResult(false);
        }

        public void Render(IProcessExecutionContext ctx);

        public void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize);

        /// <summary>
        /// Gets the recipe configuration for this process.
        /// </summary>
        /// <returns>The recipe configuration, or null if no recipe config is available.</returns>
        public IRecipeConfig GetRecipeConfig()
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

        public abstract Task<bool> Execute(IProcessExecutionContext ctx);

        public virtual Task<bool> ExecuteFailure(IProcessExecutionContext ctx) => Task.FromResult(false);

        public abstract void Render(IProcessExecutionContext ctx);
        public abstract void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize);

        protected static void AppendPlainText(Paragraph paragraph, string text, Brush foreground, double fontSize)
        {
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Run(lines[i])
                {
                    Foreground = foreground,
                    FontSize = fontSize
                });
            }
        }

        public virtual IRecipeConfig GetRecipeConfig() => null;
    }

    /// <summary>
    /// Base class for processes that use a shared recipe configuration managed by <see cref="ProcessManager"/>.
    /// </summary>
    public abstract class ProcessBase<TConfig, TRecipeConfig> : ProcessBase<TConfig>
        where TConfig : ViewModelBase, new()
        where TRecipeConfig : IRecipeConfig
    {
        public sealed override IRecipeConfig GetRecipeConfig() => ProcessManager.GetInstance().RecipeConfig.GetRequiredService<TRecipeConfig>();
    }
}
