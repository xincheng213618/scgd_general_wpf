namespace ColorVision.UI
{

    public interface IPlugin
    {
        public string Name { get; }
        public string Description { get; }
        void Execute();
    }
}
