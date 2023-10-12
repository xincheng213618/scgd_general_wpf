using ColorVision.MVVM;
using System;

namespace ColorVision.RC
{
    public class RCServiceControl : ViewModelBase
    {
        private static readonly object _locker = new();
        private static RCServiceControl _instance;
        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        private RCService rcService;
        private int heartbeatTime;
        public RCServiceControl()
        {
            _IsConnect = false;
            rcService = new RCService(new RCConfig());
            rcService.StatusChangedEventHandler += RcService_StatusChangedEventHandler;

            this.heartbeatTime = 10 * 1000;
            System.Timers.Timer hbTimer = new System.Timers.Timer(heartbeatTime);
            hbTimer.Elapsed += new System.Timers.ElapsedEventHandler(timer_KeepLive);
            hbTimer.Enabled = true;

            GC.KeepAlive(hbTimer);
        }

        private void RcService_StatusChangedEventHandler(object sender, RCServiceStatusChangedEventArgs args)
        {
            if (args.NodeStatus == MQTTMessageLib.ServiceNodeStatus.Registered) _IsConnect = true;
            else _IsConnect = false;
        }

        private void timer_KeepLive(object? sender, System.Timers.ElapsedEventArgs e)
        {
            rcService.KeepLive(heartbeatTime);
        }
        public void RCRegist()
        {
            rcService.Regist();
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
