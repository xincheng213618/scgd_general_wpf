using ColorVision.MVVM;
using ColorVision.Services;

namespace ColorVision.RC
{
    public class RCServiceControl : ViewModelBase
    {
        private static readonly object _locker = new();
        private static RCServiceControl _instance;
        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        public RCServiceControl()
        {
            _IsConnect = ServiceManager.GetInstance().rcService.IsRegisted();
            ServiceManager.GetInstance().rcService.StatusChangedEventHandler += RcService_StatusChangedEventHandler;
        }

        private void RcService_StatusChangedEventHandler(object sender, RCServiceStatusChangedEvent args)
        {
            if (args.NodeStatus == MQTTMessageLib.ServiceNodeStatus.Registered) IsConnect = true;
            else IsConnect = false;
        }

        public static RCServiceControl GetInstance()
        {
            lock (_locker)
            {
                return _instance ??= new RCServiceControl();
            }
        }
    }
}
