using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using System.Windows.Controls;

namespace ColorVision.Template
{
    public class MQTTDevice : BaseObject
    {
        public SysResourceModel SysResourceModel { get; set; }
        private SysResourceService resourceService = new SysResourceService();
        public override string Name { get => SysResourceModel.Name ?? string.Empty; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }
        public MQTTDevice() : base()
        {
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除设备" };
            menuItem.Click += (s, e) =>
            {
                this.Parent.RemoveChild(this);
                if (SysResourceModel != null)
                    resourceService.DeleteById(SysResourceModel.Id);

            };
            ContextMenu.Items.Add(menuItem);
        }
    }
}
