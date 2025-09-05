using ColorVision.Common.MVVM;
using SqlSugar;

namespace ColorVision.Database
{
    public interface IEntity
    {
        public int Id { get; set; }
    }

    public class EntityBase : IEntity
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
    }
    public class ViewEntity : ViewModelBase, IEntity
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
    }
}
