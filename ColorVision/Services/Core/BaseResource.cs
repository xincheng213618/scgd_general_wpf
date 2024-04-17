using ColorVision.Common.MVVM;
using ColorVision.Services.Dao;
using ColorVision.Services.Type;

namespace ColorVision.Services.Core
{
    public class BaseResource : BaseResourceObject
    {
        public SysResourceModel SysResourceModel { get; set; }

        public BaseResource(SysResourceModel sysResourceModel)
        {
            this.SysResourceModel = sysResourceModel;
            Name = sysResourceModel.Name ?? string.Empty;
            FilePath = sysResourceModel.Value;
            Id = sysResourceModel.Id;
            Pid = sysResourceModel.Pid;
        }

        public override void Save()
        {
            SysResourceModel.Name = Name;
            VSysResourceDao.Instance.Save(SysResourceModel);
        }

        public ServiceTypes ServiceTypes { get => (ServiceTypes)SysResourceModel.Type; }

        public string? FilePath { get; set; }
        public int Id { get; set; }
        public int? Pid { get; set; }
    }


}
