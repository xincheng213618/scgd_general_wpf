using ColorVision.UI;
using System.Threading.Tasks;

namespace ColorVision.Update
{
    public class AutoUpdateService : MainWindowInitializedBase, IBackgroundMainWindowInitializer
    {
        public override async Task Initialize()
        {
            await CombinedUpdateCoordinator.CheckForUpdatesOnStartupAsync();
        }
    }
}
