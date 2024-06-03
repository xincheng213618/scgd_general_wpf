#pragma warning disable CS8604
using ColorVision.UI;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    public class TemplateInitializer : IInitializer
    {
        private readonly IMessageUpdater _messageUpdater;

        public TemplateInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }

        public int Order => 4;

        public async Task InitializeAsync()
        {
            _messageUpdater.UpdateMessage("正在加载模板");
            await Task.Delay(10);
            Application.Current.Dispatcher.Invoke(() => TemplateControl.GetInstance());
        }
    }
}
