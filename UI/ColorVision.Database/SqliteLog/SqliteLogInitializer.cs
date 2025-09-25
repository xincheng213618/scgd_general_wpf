using ColorVision.UI;
using System.Threading.Tasks;

namespace ColorVision.Database.SqliteLog
{
    public class SqliteLogInitializer : InitializerBase
    {
        public SqliteLogInitializer() { }
        public override string Name => nameof(SqliteLogInitializer);
        public override int Order => 10;

        public override Task InitializeAsync()
        {
            SqliteLogManager.GetInstance();
            return Task.CompletedTask;
        }
    }
}
