namespace ColorVision.UI
{

    public interface IPlugin
    {
        public string Header { get; }
        public string Description { get; }
        void Execute();
    }


    public abstract class IPluginBase : IPlugin
    {
        public virtual string Header { get; set; }
        public virtual string Description { get; set; }

        public virtual void Execute()
        {

        }
    }
}
