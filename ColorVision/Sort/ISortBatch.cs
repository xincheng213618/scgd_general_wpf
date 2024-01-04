namespace ColorVision.Sort
{
    public interface ISortBatch
    {
        string? Batch { get; set; }
    }

    public interface ISortBatchID
    {
        int? BatchID { get; set; }
    }
}
