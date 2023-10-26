using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.Services;
using System;
using System.Threading.Tasks;

namespace ColorVision.RC
{
    public class RCServiceControl : ViewModelBase
    {
        private static readonly object _locker = new();
        private static RCServiceControl _instance;
        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        public RCService RCService { get;set;}

        public RCServiceControl()
        {
            RCService = RCService.GetInstance();
            _IsConnect = RCService.IsRegisted();


            RCService.StatusChangedEventHandler += RcService_StatusChangedEventHandler;

            int heartbeatTime = 10 * 1000;
            System.Timers.Timer hbTimer = new System.Timers.Timer(heartbeatTime);
            hbTimer.Elapsed += (s, e) => RCService.KeepLive(heartbeatTime);
            hbTimer.Enabled = true;
            GC.KeepAlive(hbTimer);


            MQTTControl.GetInstance().MQTTConnectChanged += (s, e) =>
            {
                Task.Run(() => RCService.Regist());



            };
        }

        private void RcService_StatusChangedEventHandler(object sender, RCServiceStatusChangedEvent args)
        {
            if (args.NodeStatus == MQTTMessageLib.ServiceNodeStatus.Registered) 
                IsConnect = true;
            else
                IsConnect = false;
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
