#pragma warning disable CS8603  

using ColorVision.MySql.DAO;

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
            this.ID =dbModel.Id;
            JsonValue = dbModel.Value;
        }

        public string? JsonValue { get; set; }
        public string? Code { get; set; }
    }
}
