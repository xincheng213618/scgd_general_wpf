using ColorVision.Services.Dao;

namespace ColorVision.Services.Templates
{
    public class ResourceParam : ParamBase
    {
        public static int TypeValue { get; set; } = 1;

        public ResourceParam()
        {

        }
        public ResourceParam(SysResourceModel dbModel)
        {
            Id = dbModel.Id;
            JsonValue = dbModel.Value;
        }

        public string? JsonValue { get; set; }
        public string? Code { get; set; }
    }
}
