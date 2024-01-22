using ColorVision.Services.Dao;

namespace ColorVision.Templates
{
    public class ResourceParam : ParamBase
    {
        public static int TypeValue { get; set; } = 1;

        public ResourceParam()
        {

        }

        public ResourceParam(SysResourceModel dbModel) 
        {
            this.Id =dbModel.Id;
            JsonValue = dbModel.Value;
        }

        public string? JsonValue { get; set; }
        public string? Code { get; set; }
    }
}
