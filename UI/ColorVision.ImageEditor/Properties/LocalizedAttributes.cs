using System;
using System.ComponentModel;
using System.Resources;

namespace ColorVision.ImageEditor.Properties
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class LocalizedDisplayNameAttribute : DisplayNameAttribute
    {
        private readonly string _resourceKey;
        private readonly ResourceManager _resourceManager;

        public LocalizedDisplayNameAttribute(Type resourceType, string resourceKey)
            : base(resourceKey)
        {
            _resourceKey = resourceKey;
            _resourceManager = new ResourceManager(resourceType);

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
                base.DisplayNameValue = resourceKey;
            }
        }

        public override string DisplayName
        {
            get
            {
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

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        private readonly string _resourceKey;
        private readonly ResourceManager _resourceManager;

        public LocalizedDescriptionAttribute(Type resourceType, string resourceKey)
            : base(resourceKey)
        {
            _resourceKey = resourceKey;
            _resourceManager = new ResourceManager(resourceType);

            try
            {
                string localizedString = _resourceManager.GetString(resourceKey);
                if (!string.IsNullOrEmpty(localizedString))
                {
                    base.DescriptionValue = localizedString;
                }
            }
            catch (Exception)
            {
                base.DescriptionValue = _resourceKey;
            }
        }

        public override string Description
        {
            get
            {
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
