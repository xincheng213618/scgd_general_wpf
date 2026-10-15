using ColorVision.Database;

namespace ColorVision.Engine.Services.Results.LightArea
{
    public class AlgResultLightAreaDao : BaseTableDao<AlgResultLightAreaModel>
    {
        public static AlgResultLightAreaDao Instance { get; set; } = new();
    }
}
