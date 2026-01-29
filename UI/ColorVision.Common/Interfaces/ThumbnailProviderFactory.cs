using ColorVision.Common.Interfaces.Assembly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.UI
{
    /// <summary>
    /// Factory for discovering and using IThumbnailProvider implementations.
    /// Discovers providers via assembly scanning and routes thumbnail requests to appropriate providers.
    /// </summary>
    public static class ThumbnailProviderFactory
    {
        private static readonly List<IThumbnailProvider> _providers = new();
        private static readonly object _lock = new();
        private static bool _initialized = false;

        /// <summary>
        /// Gets all registered thumbnail providers.
        /// </summary>
        public static IReadOnlyList<IThumbnailProvider> Providers
        {
            get
            {
                EnsureInitialized();
                return _providers.AsReadOnly();
            }
        }

        /// <summary>
        /// Ensures providers are discovered and initialized.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    // Discover IThumbnailProvider implementations from all loaded assemblies
                    var providerTypes = AssemblyService.Instance
                        .GetImplementingTypes<IThumbnailProvider>()
                        .Where(t => !t.IsAbstract && !t.IsInterface);

                    foreach (var type in providerTypes)
                    {
                        try
                        {
                            if (Activator.CreateInstance(type) is IThumbnailProvider provider)
                            {
                                _providers.Add(provider);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to create IThumbnailProvider instance of {type.Name}: {ex.Message}");
                        }
                    }

                    // Sort by Order (lower values first)
                    _providers.Sort((a, b) => a.Order.CompareTo(b.Order));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ThumbnailProviderFactory initialization error: {ex.Message}");
                }

                _initialized = true;
            }
        }

        /// <summary>
        /// Finds a provider that can handle the given file.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>The first matching provider, or null if none can handle the file</returns>
        public static IThumbnailProvider? GetProvider(string filePath)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(filePath))
                return null;

            return _providers.FirstOrDefault(p => p.CanHandle(filePath));
        }

        /// <summary>
        /// Checks if any provider can handle the given file extension.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if a custom provider exists for this file type</returns>
        public static bool HasCustomProvider(string filePath)
        {
            return GetProvider(filePath) != null;
        }

        /// <summary>
        /// Generates a thumbnail using the appropriate provider.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="maxSize">Maximum thumbnail dimension</param>
        /// <returns>BitmapSource thumbnail, or null if no provider can handle the file or generation fails</returns>
        public static async Task<BitmapSource?> GenerateThumbnailAsync(string filePath, int maxSize)
        {
            var provider = GetProvider(filePath);
            if (provider == null)
                return null;

            return await provider.GenerateThumbnailAsync(filePath, maxSize);
        }

        /// <summary>
        /// Gets image dimensions using the appropriate provider.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Tuple of (width, height), or (0, 0) if no provider can handle the file</returns>
        public static (int width, int height) GetImageDimensions(string filePath)
        {
            var provider = GetProvider(filePath);
            if (provider == null)
                return (0, 0);

            return provider.GetImageDimensions(filePath);
        }

        /// <summary>
        /// Gets all supported file extensions from registered providers.
        /// </summary>
        public static IEnumerable<string> GetSupportedExtensions()
        {
            EnsureInitialized();

            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in _providers)
            {
                var attr = provider.GetType().GetCustomAttributes(typeof(FileExtensionAttribute), false)
                    .FirstOrDefault() as FileExtensionAttribute;

                if (attr?.Extensions != null)
                {
                    foreach (var ext in attr.Extensions)
                    {
                        extensions.Add(ext);
                    }
                }
            }

            return extensions;
        }

        /// <summary>
        /// Manually registers a thumbnail provider (for testing or manual registration).
        /// </summary>
        public static void RegisterProvider(IThumbnailProvider provider)
        {
            lock (_lock)
            {
                if (!_providers.Contains(provider))
                {
                    _providers.Add(provider);
                    _providers.Sort((a, b) => a.Order.CompareTo(b.Order));
                }
            }
        }

        /// <summary>
        /// Clears all registered providers (mainly for testing).
        /// </summary>
        public static void ClearProviders()
        {
            lock (_lock)
            {
                _providers.Clear();
                _initialized = false;
            }
        }
    }
}
