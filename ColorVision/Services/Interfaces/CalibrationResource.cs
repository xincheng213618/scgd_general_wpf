#pragma warning disable CS8603,CS0649
using ColorVision.MVVM;
using ColorVision.Services.Dao;
using ColorVision.Sorts;

namespace ColorVision.Services.Interfaces
{
    public class CalibrationResource : BaseResource, ISortID, ISortFilePath
    {
        public CalibrationResource(SysResourceModel sysResourceModel)
        {
            this.SysResourceModel = sysResourceModel;
            Name = sysResourceModel.Name ?? string.Empty;
            FilePath = sysResourceModel.Value;
            Id = sysResourceModel.Id;
            Pid = sysResourceModel.Pid;
        }

        public string? FilePath { get; set; }
        public int Id { get; set; }
        public int? Pid { get; set; }
    }


}
