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
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        [Column("id")]
        public int Id { get; set; }  
    }
    public class VPKModel : ViewModelBase ,IPKModel
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        [Column("id")]
        public int Id { get; set; }
    }
}
