﻿using ColorVision.Services.Dao;

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

        public string? FilePath { get; set; }
        public int Id { get; set; }
        public int? Pid { get; set; }
    }


}
