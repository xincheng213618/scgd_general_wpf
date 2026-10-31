namespace ColorVision.UI
{

    [System.Obsolete("No host calls IPlugin. Contribute through the extension interfaces (IMenuItem, IInitializer, IConfig, ...) instead.")]
    public interface IPlugin
    {
        public string Header { get; }
        public string Description { get; }
        void Execute();
    }


    [System.Obsolete("No host calls IPlugin. Contribute through the extension interfaces (IMenuItem, IInitializer, IConfig, ...) instead.")]
    public abstract class IPluginBase : IPlugin
    {
        public virtual string Header { get; set; }
        public virtual string Description { get; set; }

        public virtual void Execute()
        {

        }
    }
}
