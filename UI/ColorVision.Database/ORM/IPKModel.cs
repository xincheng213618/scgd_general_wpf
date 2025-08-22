using ColorVision.Common.MVVM;
using SqlSugar;

namespace ColorVision.Engine.MySql.ORM
{
    public interface IPKModel
    {
        public int Id { get; set; }
    }

    public class PKModel : IPKModel
    {
        [SugarColumn(ColumnName ="id",IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }  
    }
    public class VPKModel : ViewModelBase ,IPKModel
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
    }
}
