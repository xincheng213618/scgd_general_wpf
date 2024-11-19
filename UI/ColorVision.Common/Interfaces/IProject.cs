namespace ColorVision.UI
{
    public interface IProject
    {
        public string? Header { get; }
        public string? UpdateUrl { get; }

        public void Execute();
    }

    public abstract class IProjectBase : IProject
    {
        public virtual string? Header { get; set; }
        public virtual string? UpdateUrl { get; set; }

        public virtual void Execute()
        {
            
        }
    }
}
