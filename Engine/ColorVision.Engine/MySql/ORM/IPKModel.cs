using ColorVision.Common.MVVM;

namespace ColorVision.Engine.MySql.ORM
{
    public interface IPKModel
    {
        public int Id { get; set; }
    }

    public class PKModel : IPKModel
    {
        [Column("id")]
        public int Id { get; set; }  
    }
    public class VPKModel : ViewModelBase ,IPKModel
    {
        [Column("id")]
        public int Id { get; set; }
    }
}
