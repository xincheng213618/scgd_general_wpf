namespace ColorVision.MySql
{
    public interface IPKModel
    {
        public int PKId { get; set; }
    }

    public class PKModel : IPKModel
    {
        public int Id { get; set; }

        public int PKId { get => Id; set => Id = value; }
    }
}
