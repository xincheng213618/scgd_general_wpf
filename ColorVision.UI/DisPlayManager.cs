using System.Collections.ObjectModel;

namespace ColorVision.UI
{

    public class DisPlayManagerConfig : IConfig
    {
        public static DisPlayManagerConfig Instance => ConfigHandler.GetInstance().GetRequiredService<DisPlayManagerConfig>();
        public Dictionary<string, int> PlayControls { get; set; } = new Dictionary<string, int>();
    }

    public class DisPlayManager
    {
        private static DisPlayManager _instance;
        private static readonly object _locker = new();
        public static DisPlayManager GetInstance() { lock (_locker) { return _instance ??= new DisPlayManager(); } }
        public ObservableCollection<IDisPlayControl> IDisPlayControls { get; private set; }

        private DisPlayManager()
        {
            IDisPlayControls = new ObservableCollection<IDisPlayControl>();
        }


    }
}
