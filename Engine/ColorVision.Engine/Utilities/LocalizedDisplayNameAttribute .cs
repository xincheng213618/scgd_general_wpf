using System;
using System.ComponentModel;
using System.Resources;


namespace ColorVision.Engine.Utilities
{
  
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class LocalizedDisplayNameAttribute : DisplayNameAttribute
    {
        private readonly string _resourceKey;
        private readonly ResourceManager _resourceManager;

        public LocalizedDisplayNameAttribute(Type resourceType, string resourceKey)
            : base(resourceKey) // 传递资源键作为默认显示名称
        {
            _resourceKey = resourceKey;
            _resourceManager = new ResourceManager(resourceType);

            // 尝试立即获取本地化字符串，如失败则使用资源键
            try
            {
                string localizedString = _resourceManager.GetString(resourceKey);
                if (!string.IsNullOrEmpty(localizedString))
                {
                    base.DisplayNameValue = localizedString;
                }
            }
            catch (Exception)
            {
                // 如果资源查找失败，保持资源键作为回退值
                base.DisplayNameValue = resourceKey;
            }
        }

        public override string DisplayName
        {
            get
            {
                try
                {
                    // 每次访问时都从资源管理器获取最新值，支持运行时语言切换
                    string localizedString = _resourceManager.GetString(_resourceKey);
                    return string.IsNullOrEmpty(localizedString) ? _resourceKey : localizedString;
                }
                catch (Exception)
                {
                    return _resourceKey;
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        private readonly string _resourceKey;
        private readonly ResourceManager _resourceManager;
        private bool _isInitialized;

        public LocalizedDescriptionAttribute(Type resourceType, string resourceKey)
            : base(resourceKey) // 将资源键作为默认描述
        {
            _resourceKey = resourceKey;
            _resourceManager = new ResourceManager(resourceType);

            // 立即尝试初始化描述值
            InitializeDescription();
        }

        private void InitializeDescription()
        {
            if (_isInitialized) return;

            try
            {
                string localizedString = _resourceManager.GetString(_resourceKey);
                if (!string.IsNullOrEmpty(localizedString))
                {
                    base.DescriptionValue = localizedString;
                }
                _isInitialized = true;
            }
            catch (Exception)
            {
                // 如果资源查找失败，保持资源键作为回退值
                base.DescriptionValue = _resourceKey;
                _isInitialized = true;
            }
        }

        public override string Description
        {
            get
            {
                // 每次访问时都尝试获取最新的本地化字符串
                try
                {
                    string localizedString = _resourceManager.GetString(_resourceKey);
                    return string.IsNullOrEmpty(localizedString) ? _resourceKey : localizedString;
                }
                catch (Exception)
                {
                    return _resourceKey;
                }
            }
        }
    }
}
