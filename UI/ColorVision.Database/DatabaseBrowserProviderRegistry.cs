#pragma warning disable CA1510
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Database
{
    public static class DatabaseBrowserProviderRegistry
    {
        private static readonly object Locker = new();
        private static readonly List<IDatabaseBrowserProvider> Providers = new();
        private static bool _defaultsRegistered;

        public static void Register(IDatabaseBrowserProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            lock (Locker)
            {
                Providers.RemoveAll(item => string.Equals(item.ProviderId, provider.ProviderId, StringComparison.OrdinalIgnoreCase));
                Providers.Add(provider);
            }
        }

        public static IReadOnlyList<IDatabaseBrowserProvider> GetProviders()
        {
            EnsureDefaultProviders();

            lock (Locker)
            {
                return Providers.ToList();
            }
        }

        public static IDatabaseBrowserProvider? GetProvider(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId)) return null;

            EnsureDefaultProviders();

            lock (Locker)
            {
                return Providers.FirstOrDefault(item => string.Equals(item.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static void EnsureDefaultProviders()
        {
            if (_defaultsRegistered) return;

            lock (Locker)
            {
                if (_defaultsRegistered) return;

                RegisterCore(MySqlControl.CreateBrowserProvider());
                _defaultsRegistered = true;
            }
        }

        private static void RegisterCore(IDatabaseBrowserProvider provider)
        {
            Providers.RemoveAll(item => string.Equals(item.ProviderId, provider.ProviderId, StringComparison.OrdinalIgnoreCase));
            Providers.Add(provider);
        }
    }
}
