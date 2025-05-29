﻿namespace ColorVision.UI.SocketProtocol
{

    public class SocketInitializer : InitializerBase
    {
        private readonly IMessageUpdater _messageUpdater;

        public SocketInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }
        public override string Name => nameof(SocketInitializer);
        public override int Order => 5;

        public override Task InitializeAsync()
        {
            if (SocketConfig.Instance.IsServerEnabled )
            {
                _messageUpdater.Update("启动通讯协议");
                SocketManager.GetInstance().StartServer();
            }
            SocketConfig.Instance.ServerEnabledChanged += (s, e) =>
            {
                if (e)
                {
                    SocketManager.GetInstance().StartServer();
                }
                else
                {
                    SocketManager.GetInstance().StopServer();
                }
            };
            return Task.CompletedTask;
        }
    }
}
