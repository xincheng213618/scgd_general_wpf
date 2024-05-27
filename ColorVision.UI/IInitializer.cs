using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.UI
{
    public interface IMessageUpdater
    {
        void UpdateMessage(string message);
    }

    public class MessageUpdater : IMessageUpdater
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MessageUpdater));
        public void UpdateMessage(string message)
        {
            log.Info(message);
        }
    }

    public interface IInitializer
    {
        public int Order { get; }
        Task InitializeAsync();
    }
}
