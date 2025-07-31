namespace ColorVision.UI
{
    public interface IProject
    {
        string? Header { get; }
        string Description { get; }

        void Execute();
    }

    public abstract class IProjectBase : IProject
    {
        public virtual string? Header { get; set; }
        public virtual string? UpdateUrl { get; set; }
        public virtual string Description { get; set; }

        public virtual void Execute()
        {
            
        }
    }
}
