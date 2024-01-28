#pragma warning disable CS8603,CS0649
using ColorVision.Services.Dao;
using ColorVision.Services.Devices;

namespace ColorVision.Services.Interfaces
{
    public class BaseResource : BaseResourceObject
    {
        public SysResourceModel SysResourceModel { get; set; }
    }


}
