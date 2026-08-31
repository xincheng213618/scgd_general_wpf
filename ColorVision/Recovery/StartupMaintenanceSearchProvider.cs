using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;

namespace ColorVision.Recovery;

/// <summary>Search-only maintenance entries. Catalog discovery never creates a window or restarts the app.</summary>
public sealed class StartupMaintenanceSearchProvider : ISearchProvider
{
    private readonly Action<StartupMaintenanceMode> _request;

    public StartupMaintenanceSearchProvider() : this(StartupMaintenanceController.Request) { }

    internal StartupMaintenanceSearchProvider(Action<StartupMaintenanceMode> request)
        => _request = request ?? throw new ArgumentNullException(nameof(request));

    public IEnumerable<ISearch> GetSearchItems()
    {
        yield return new SearchMeta
        {
            Type = SearchType.Menu,
            GuidId = "maintenance:setup-wizard",
            Header = StartupMaintenanceText.Get("WizardTitle"),
            Description = StartupMaintenanceText.Get("WizardDescription"),
            CategoryKey = "Commands",
            Category = StartupMaintenanceText.Get("Category"),
            Aliases = ["向导", "初始化", "配置向导", "首次配置", "setup", "wizard", "initialization", "WizardWindow"],
            Command = new RelayCommand(_ => _request(StartupMaintenanceMode.SetupWizard)),
        };
        yield return new SearchMeta
        {
            Type = SearchType.Menu,
            GuidId = "maintenance:startup-recovery",
            Header = StartupMaintenanceText.Get("RecoveryTitle"),
            Description = StartupMaintenanceText.Get("RecoveryDescription"),
            CategoryKey = "Commands",
            Category = StartupMaintenanceText.Get("Category"),
            Aliases = ["恢复", "启动恢复", "故障", "修复", "安全启动", "recovery", "repair", "safe mode", "StartupRecoveryWindow"],
            Command = new RelayCommand(_ => _request(StartupMaintenanceMode.Recovery)),
        };
    }
}
