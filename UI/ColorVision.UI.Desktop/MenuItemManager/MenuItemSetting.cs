using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.ComponentModel;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    public class MenuItemSetting : ViewModelBase
    {
        public string TargetName { get => _targetName; set => SetProperty(ref _targetName, value); }
        private string _targetName = string.Empty;

        public string GuidId { get => _guidId; set => SetProperty(ref _guidId, value); }
        private string _guidId = string.Empty;

        public string? OwnerGuid { get => _ownerGuid; set => SetProperty(ref _ownerGuid, value); }
        private string? _ownerGuid;

        [Browsable(false)]
        public string? Header { get => _header; set => SetProperty(ref _header, value); }
        private string? _header;

        [Browsable(false)]
        public int DefaultOrder { get => _defaultOrder; set => SetProperty(ref _defaultOrder, value); }
        private int _defaultOrder;

        [DisplayName("Visible")]
        public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
        private bool _isVisible = true;

        [DisplayName("Order")]
        public int? OrderOverride { get => _orderOverride; set => SetProperty(ref _orderOverride, value); }
        private int? _orderOverride;

        [DisplayName("OwnerGuid Override")]
        public string? OwnerGuidOverride { get => _ownerGuidOverride; set => SetProperty(ref _ownerGuidOverride, value); }
        private string? _ownerGuidOverride;

        [Browsable(false)]
        public string? SourceType { get => _sourceType; set => SetProperty(ref _sourceType, value); }
        private string? _sourceType;

        [Browsable(false)]
        public string? SourceAssembly { get => _sourceAssembly; set => SetProperty(ref _sourceAssembly, value); }
        private string? _sourceAssembly;
    }

    /// <summary>
    /// Persisted menu customization. Catalog metadata stays in <see cref="MenuItemSetting"/>
    /// and is rebuilt from the currently available menu items when the editor opens.
    /// </summary>
    public sealed class MenuItemOverride
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? TargetName { get; set; }

        public string GuidId { get; set; } = string.Empty;

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsVisible { get; set; } = true;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? OrderOverride { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? OwnerGuidOverride { get; set; }
    }
}
