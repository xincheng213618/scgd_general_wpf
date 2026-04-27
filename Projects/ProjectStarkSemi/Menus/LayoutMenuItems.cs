using ColorVision.UI.Menus;
using ProjectStarkSemi.Conoscope;
using System.Windows;

namespace ProjectStarkSemi.Menus
{
    public class MenuSaveLayout : ConoscopeMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "保存窗口布局";
        public override int Order => 100;

        public override void Execute()
        {
            ConoscopeModuleService.ActiveView?.LayoutManager?.SaveLayout();
        }
    }

    public class MenuApplyLayout : ConoscopeMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "应用窗口布局";
        public override int Order => 101;

        public override void Execute()
        {
            var layoutManager = ConoscopeModuleService.ActiveView?.LayoutManager;
            if (layoutManager == null) return;
            if (!layoutManager.LoadLayout())
            {
                layoutManager.ResetLayout();
            }
        }
    }

    public class MenuResetLayout : ConoscopeMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "重置窗口布局";
        public override int Order => 102;

        public override void Execute()
        {
            ConoscopeModuleService.ActiveView?.LayoutManager?.ResetLayout();
        }
    }

    public class MenuToggleControlPanel : ConoscopeMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "切换控制面板";
        public override int Order => 110;

        public override void Execute()
        {
            ConoscopeModuleService.ActiveView?.LayoutManager?.TogglePanel("ControlPanel");
        }
    }

    public class MenuToggleChannelPanel : ConoscopeMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "切换通道面板";
        public override int Order => 111;

        public override void Execute()
        {
            ConoscopeModuleService.ActiveView?.LayoutManager?.TogglePanel("ChannelPanel");
        }
    }

    public class MenuToggleReferencePlot : ConoscopeMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "切换参考曲线";
        public override int Order => 112;

        public override void Execute()
        {
            ConoscopeModuleService.ActiveView?.LayoutManager?.TogglePanel("ReferencePlot");
        }
    }

    public class MenuToggleSettingPanel : ConoscopeMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "切换设置面板";
        public override int Order => 113;

        public override void Execute()
        {
            ConoscopeModuleService.ActiveView?.LayoutManager?.TogglePanel("SettingPanel");
        }
    }
}
