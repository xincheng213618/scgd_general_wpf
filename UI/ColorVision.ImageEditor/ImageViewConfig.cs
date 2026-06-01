using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Properties;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace ColorVision.ImageEditor
{

    public interface IImageEditorConfig
    {

    }

    public class ImageViewConfig:ViewModelBase
    {
        private sealed class ImageViewPropertyState
        {
            public ImageViewPropertyScope Scope { get; init; }
            public string? Owner { get; init; }
            public string? Description { get; init; }
        }

        public RelayCommand ClearCommand { get; set; }

        public ImageViewConfig()
        {
            Configs = new Dictionary<Type, IImageEditorConfig>();

            ClearCommand = new RelayCommand(o => Cleared?.Invoke(this,new EventArgs()));    
        }

        public Dictionary<Type, IImageEditorConfig> Configs { get; set; }

        public T GetRequiredService<T>() where T : IImageEditorConfig
        {
            var type = typeof(T);

            if (Configs.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            if (Activator.CreateInstance(type) is IImageEditorConfig defaultConfig)
            {
                Configs[type] = defaultConfig;
            }
            // 此处递归调用是为了确保缓存和异常处理逻辑一致
            return GetRequiredService<T>();
        }
        public event EventHandler Cleared;
        [JsonIgnore]
        public Dictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();

        [JsonIgnore]
        private readonly Dictionary<string, ImageViewPropertyState> _propertyStates = new Dictionary<string, ImageViewPropertyState>();

        public void ClearProperties()
        {
            FilePath = string.Empty;
            Properties.Clear();
            _propertyStates.Clear();
            Cleared?.Invoke(this, new EventArgs());
        }



        public void AddProperties(string Key,object? Value)
            => SetProperty(Key, Value, ImageViewPropertyScope.Legacy);

        public void SetImageMetadata(string key, object? value, string? owner = null, string? description = null)
            => SetProperty(key, value, ImageViewPropertyScope.ImageMetadata, owner, description);

        public void SetViewState(string key, object? value, string? owner = null, string? description = null)
            => SetProperty(key, value, ImageViewPropertyScope.ViewState, owner, description);

        public void SetOpenerRuntime(string key, object? value, string? owner = null, string? description = null)
            => SetProperty(key, value, ImageViewPropertyScope.OpenerRuntime, owner, description);

        public void SetProperty(string key, object? value, ImageViewPropertyScope scope, string? owner = null, string? description = null)
        {
            if (!Properties.TryAdd(key, value))
                Properties[key] = value;

            _propertyStates[key] = new ImageViewPropertyState
            {
                Scope = scope,
                Owner = owner,
                Description = description,
            };
        }

        public T? GetProperties<T>(string Key)
        {
            if (Properties.TryGetValue(Key, out var value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
            }
            return default;
        }

        public IReadOnlyList<ImageViewPropertyEntry> GetPropertyEntries()
        {
            return Properties.Select(item =>
            {
                _propertyStates.TryGetValue(item.Key, out ImageViewPropertyState? state);
                return new ImageViewPropertyEntry
                {
                    Key = item.Key,
                    Value = item.Value,
                    Scope = state?.Scope ?? ImageViewPropertyScope.Legacy,
                    Owner = state?.Owner,
                    Description = state?.Description,
                };
            }).ToList();
        }

        internal static string FormatPropertyValue(object? value)
        {
            if (value is IEnumerable enumerable && value is not string)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(item?.ToString() ?? "null");
                }
                return $"[{string.Join(", ", items)}]";
            }
            return value?.ToString() ?? "null";
        }
        public string GetPropertyString()
        {
            var sb = new StringBuilder();
            foreach (var group in GetPropertyEntries().GroupBy(entry => entry.Scope).OrderBy(group => GetScopeSortOrder(group.Key)))
            {
                sb.AppendLine($"[{GetScopeDisplayName(group.Key)}]");
                foreach (var item in group.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                {
                    sb.Append(item.Key);
                    sb.Append(':');
                    sb.Append(FormatPropertyValue(item.Value));
                    if (!string.IsNullOrWhiteSpace(item.Owner))
                    {
                        sb.Append(" (Owner=");
                        sb.Append(item.Owner);
                        sb.Append(')');
                    }
                    if (!string.IsNullOrWhiteSpace(item.Description))
                    {
                        sb.Append(" // ");
                        sb.Append(item.Description);
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
            return sb.ToString();

        }

        internal static string GetScopeDisplayName(ImageViewPropertyScope scope)
        {
            return scope switch
            {
                ImageViewPropertyScope.ImageMetadata => ColorVision.ImageEditor.Properties.Resources.ImageView_Scope_ImageMetadata,
                ImageViewPropertyScope.ViewState => ColorVision.ImageEditor.Properties.Resources.ImageView_Scope_ViewState,
                ImageViewPropertyScope.OpenerRuntime => ColorVision.ImageEditor.Properties.Resources.ImageView_Scope_OpenerRuntime,
                _ => ColorVision.ImageEditor.Properties.Resources.ImageView_Scope_Legacy,
            };
        }

        internal static string GetScopeDescription(ImageViewPropertyScope scope)
        {
            return scope switch
            {
                ImageViewPropertyScope.ImageMetadata => ColorVision.ImageEditor.Properties.Resources.ImageView_ScopeDesc_ImageMetadata,
                ImageViewPropertyScope.ViewState => ColorVision.ImageEditor.Properties.Resources.ImageView_ScopeDesc_ViewState,
                ImageViewPropertyScope.OpenerRuntime => ColorVision.ImageEditor.Properties.Resources.ImageView_ScopeDesc_OpenerRuntime,
                _ => ColorVision.ImageEditor.Properties.Resources.ImageView_ScopeDesc_Legacy,
            };
        }

        internal static int GetScopeSortOrder(ImageViewPropertyScope scope)
        {
            return scope switch
            {
                ImageViewPropertyScope.ImageMetadata => 0,
                ImageViewPropertyScope.ViewState => 1,
                ImageViewPropertyScope.OpenerRuntime => 2,
                _ => 99,
            };
        }


        [JsonIgnore]
        public string FilePath { get => GetProperties<string>(ImageViewPropertyKeys.FilePath); set { SetImageMetadata(ImageViewPropertyKeys.FilePath, value, nameof(ImageViewConfig), ColorVision.ImageEditor.Properties.Resources.ImageView_MetadataDesc_FilePath); OnPropertyChanged(); } }


        public event EventHandler<bool> LayoutUpdatedChanged;
        [DisplayName("自动刷新")]
        public bool IsLayoutUpdated
        {
            get => _IsLayoutUpdated;
            set
            {
                if (_IsLayoutUpdated == value) return;
                _IsLayoutUpdated = value;
                OnPropertyChanged();
                LayoutUpdatedChanged?.Invoke(this, _IsLayoutUpdated);
            }
        }
        private bool _IsLayoutUpdated = true;

        public event EventHandler<double> DrawingTextFontSizeChanged;
        [DisplayName("绘制文字大小")]
        public double DrawingTextFontSize { get => _DrawingTextFontSize; set { _DrawingTextFontSize = Math.Max(0, value); OnPropertyChanged(); DrawingTextFontSizeChanged?.Invoke(this, _DrawingTextFontSize); } }
        private double _DrawingTextFontSize;


        public event EventHandler<bool> ShowTextChanged;
        [DisplayName("显示文字")]
        public bool IsShowText { get => _IsShowText; set { _IsShowText = value; OnPropertyChanged(); ShowTextChanged?.Invoke(this, _IsShowText); } }
        private bool _IsShowText = true;

        public event EventHandler<bool> ShowMsgChanged;
        [DisplayName("显示消息")]
        public bool IsShowMsg { get => _IsShowMsg; set { _IsShowMsg = value; OnPropertyChanged(); ShowMsgChanged?.Invoke(this, _IsShowMsg); } }
        private bool _IsShowMsg = true;

        // Toolbar visibility properties
        public bool IsToolBarAlVisible { get => _IsToolBarAlVisible; set { _IsToolBarAlVisible = value; OnPropertyChanged(); } }
        private bool _IsToolBarAlVisible = true;

        public bool IsToolBarDrawVisible { get => _IsToolBarDrawVisible; set { _IsToolBarDrawVisible = value; OnPropertyChanged(); } }
        private bool _IsToolBarDrawVisible;

        public bool IsToolBarTopVisible { get => _IsToolBarTopVisible; set { _IsToolBarTopVisible = value; OnPropertyChanged(); } }
        private bool _IsToolBarTopVisible = true;

        public bool IsToolBarLeftVisible { get => _IsToolBarLeftVisible; set { _IsToolBarLeftVisible = value; OnPropertyChanged(); } }
        private bool _IsToolBarLeftVisible = true;

        public bool IsToolBarRightVisible { get => _IsToolBarRightVisible; set { _IsToolBarRightVisible = value; OnPropertyChanged(); } }
        private bool _IsToolBarRightVisible = true;

    }
}
