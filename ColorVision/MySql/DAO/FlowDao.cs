using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class FlowModel : IBaseModel
    {
        public FlowModel(string text, int tenantId)
        {
            TenantId = tenantId;
        }

        public int Id { get; set; }
        public string? Name { get; set; }
        public int TenantId { get; set; }
        public int GetPK()
        {
            return Id;
        }

        public void SetPK(int id)
        {
            Id = id;
        }
    }
    internal class FlowDao
    {
    }
}
