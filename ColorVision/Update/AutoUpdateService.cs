using ColorVision.UI;
using System.Threading.Tasks;

namespace ColorVision.Update
{
    public class AutoUpdateService : MainWindowInitializedBase
    {
        public override async Task Initialize()
        {
            await CombinedUpdateCoordinator.CheckForUpdatesOnStartupAsync();
        }
    }
}
