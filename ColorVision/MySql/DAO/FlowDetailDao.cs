using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class FlowDetailModel : IBaseModel
    {
        public int Id { get; set; }

        public int GetPK()
        {
            return Id;
        }

        public void SetPK(int id)
        {
            Id = id;
        }
    }

    public class FlowDetailDao : BaseModDetailDao<FlowDetailModel>
    {
        public FlowDetailDao() : base("t_scgd_mod_param_detail", "id")
        {
        }
    }
}