namespace ColorVision.MySql
{
    public interface IPKModel
    {
        int GetPK();
        void SetPK(int id);
    }

    public class PKModel : IPKModel
    {
        public int Id { get; set; }

        public int GetPK() => Id;
        public void SetPK(int id) => Id = id;
    }
}
