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

    public interface IInitializer
    {
        public int Order { get; }
        Task InitializeAsync();
    }
}
