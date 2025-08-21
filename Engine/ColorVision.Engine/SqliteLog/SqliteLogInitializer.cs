using ColorVision.UI;
using System.Threading.Tasks;

namespace ColorVision.Engine.SqliteLog
{
    public class SqliteLogInitializer : InitializerBase
    {
        private readonly IMessageUpdater _messageUpdater;

        public SqliteLogInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }
        public override string Name => nameof(SqliteLogInitializer);
        public override int Order => 10;

        public override Task InitializeAsync()
        {
            SqliteLogManager.GetInstance();
            return Task.CompletedTask;
        }
    }
}
