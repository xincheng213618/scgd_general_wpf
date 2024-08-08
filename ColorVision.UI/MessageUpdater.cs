using log4net;

namespace ColorVision.UI
{
    public class MessageUpdater : IMessageUpdater
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MessageUpdater));
        public void UpdateMessage(string message)
        {
            log.Info(message);
        }
    }
}
