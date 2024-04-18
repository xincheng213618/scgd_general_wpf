using ColorVision.Common.Sorts;
using ColorVision.Services.Dao;

namespace ColorVision.Services.Core
{
    public class CalibrationResource : BaseFileResource, ISortID, ISortFilePath
    {
        public CalibrationResource(SysResourceModel sysResourceModel) : base(sysResourceModel) 
        { 
        }

        public int IdShow { get; set; }
    }
}
