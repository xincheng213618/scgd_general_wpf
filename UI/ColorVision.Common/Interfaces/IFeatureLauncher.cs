namespace ColorVision.UI
{
    public interface IFeatureLauncher
    {
        string? Header { get; }
        string Description { get; }

        void Execute();
    }

    public abstract class IFeatureLauncherBase : IFeatureLauncher
    {
        public virtual string? Header { get; set; }
        public virtual string? UpdateUrl { get; set; }
        public virtual string Description { get; set; }

        public virtual void Execute()
        {
            
        }
    }
}
