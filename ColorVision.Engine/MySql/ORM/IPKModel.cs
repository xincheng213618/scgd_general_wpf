namespace ColorVision.Engine.MySql.ORM
{
    public interface IPKModel
    {
        public int Id { get; set; }
    }

    public class PKModel : IPKModel
    {
        public int Id { get; set; }  
    }
}
