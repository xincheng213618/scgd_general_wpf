using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public sealed class CopilotAttachmentItem : ViewModelBase
    {
        private const int PreviewDecodePixelWidth = 256;
        private const int MaximumConcurrentPreviewLoads = 2;
        public const int MaximumStoredTextCharacters = 12_000;
        private const string StoredTextTruncationMarker = "\n...<attachment truncated>";
        private static readonly SemaphoreSlim PreviewLoadSlots = new(MaximumConcurrentPreviewLoads, MaximumConcurrentPreviewLoads);
        private readonly object _previewSync = new();

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, NormalizeText(value));
        }
        private string _id = Guid.NewGuid().ToString("N");

        public CopilotAttachmentType Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    ResetPreviewImage();
                    OnPropertyChanged(nameof(BadgeText));
                    OnPropertyChanged(nameof(IconGlyph));
                    OnPropertyChanged(nameof(DisplayLabel));
                }
            }
        }
        private CopilotAttachmentType _type;

        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, NormalizeText(value)))
                    OnPropertyChanged(nameof(DisplayLabel));
            }
        }
        private string _title = string.Empty;

        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value?.Trim() ?? string.Empty))
                {
                    ResetPreviewImage();
                    OnPropertyChanged(nameof(DisplayLabel));
                    OnPropertyChanged(nameof(TooltipText));
                }
            }
        }
        private string _value = string.Empty;

        public string Source
        {
            get => _source;
            set
            {
                if (SetProperty(ref _source, value?.Trim() ?? string.Empty))
                    OnPropertyChanged(nameof(TooltipText));
            }
        }
        private string _source = string.Empty;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }
        private DateTime _createdAt = DateTime.Now;

        [JsonIgnore]
        public string BadgeText => Type switch
        {
            CopilotAttachmentType.File => CopilotUiText.FileBadge,
            CopilotAttachmentType.Image => CopilotUiText.ImageBadge,
            CopilotAttachmentType.WebPage => CopilotUiText.WebPageBadge,
            _ => CopilotUiText.ContextBadge,
        };

        [JsonIgnore]
        public string IconGlyph => Type switch
        {
            CopilotAttachmentType.File => "\uE8A5",
            CopilotAttachmentType.Image => "\uEB9F",
            CopilotAttachmentType.WebPage => "\uE774",
            _ => "\uE723",
        };

        [JsonIgnore]
        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                    return Title;

                if (Type == CopilotAttachmentType.File || Type == CopilotAttachmentType.Image)
                    return Path.GetFileName(Value);

                if (Type == CopilotAttachmentType.WebPage)
                    return TryGetHostLabel(Source);

                return BuildPreview(Value, 20);
            }
        }

        [JsonIgnore]
        public string TooltipText => Type == CopilotAttachmentType.WebPage && !string.IsNullOrWhiteSpace(Source)
            ? Source
            : Value;

        [JsonIgnore]
        public ImageSource? PreviewImage
        {
            get
            {
                string imagePath;
                int generation;
                lock (_previewSync)
                {
                    if (Type != CopilotAttachmentType.Image || string.IsNullOrWhiteSpace(Value))
                        return null;

                    imagePath = Value;
                    if (string.Equals(_previewImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
                        return _previewImage;
                    if (string.Equals(_previewLoadingPath, imagePath, StringComparison.OrdinalIgnoreCase))
                        return null;

                    _previewLoadingPath = imagePath;
                    generation = ++_previewGeneration;
                }

                _ = LoadPreviewImageAsync(imagePath, generation);
                return null;
            }
        }

        private async Task LoadPreviewImageAsync(string imagePath, int generation)
        {
            ImageSource? previewImage = null;
            var enteredLoadSlot = false;
            try
            {
                await PreviewLoadSlots.WaitAsync().ConfigureAwait(false);
                enteredLoadSlot = true;
                var bytes = await CopilotImagePayloadLoader.LoadImageBytesAsync(
                    imagePath,
                    Path.GetFileName(imagePath),
                    CancellationToken.None).ConfigureAwait(false);
                using var stream = new MemoryStream(bytes, writable: false);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.DecodePixelWidth = PreviewDecodePixelWidth;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                previewImage = image;
            }
            catch
            {
            }
            finally
            {
                if (enteredLoadSlot)
                    PreviewLoadSlots.Release();
            }

            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.CheckAccess())
                {
                    ApplyPreviewImage(imagePath, generation, previewImage);
                    return;
                }
                if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                    return;

                var operation = dispatcher.InvokeAsync(
                    () => ApplyPreviewImage(imagePath, generation, previewImage),
                    DispatcherPriority.Background);
                await operation.Task.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private void ApplyPreviewImage(string imagePath, int generation, ImageSource? previewImage)
        {
            lock (_previewSync)
            {
                if (generation != _previewGeneration
                    || Type != CopilotAttachmentType.Image
                    || !string.Equals(Value, imagePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _previewImage = previewImage;
                _previewImagePath = imagePath;
                _previewLoadingPath = string.Empty;
            }

            OnPropertyChanged(nameof(PreviewImage));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(ImageFallbackText));
        }

        [JsonIgnore]
        public bool HasPreviewImage => PreviewImage != null;

        [JsonIgnore]
        public bool IsImage => Type == CopilotAttachmentType.Image;

        [JsonIgnore]
        public bool IsStoredImageFile => Type == CopilotAttachmentType.Image && !string.IsNullOrWhiteSpace(Value);

        [JsonIgnore]
        public string ImageFallbackText => HasPreviewImage ? string.Empty : CopilotUiText.ImagePreviewUnavailable;

        [JsonIgnore]
        public string ImageMetaText => CreatedAt.ToString("M/d HH:mm");

        private ImageSource? _previewImage;

        private string _previewImagePath = string.Empty;

        private string _previewLoadingPath = string.Empty;

        private int _previewGeneration;

        public bool EnsureValid()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (_value == null)
            {
                Value = string.Empty;
                changed = true;
            }
            else if (Type is CopilotAttachmentType.Context or CopilotAttachmentType.WebPage)
            {
                var normalizedValue = NormalizeStoredText(_value);
                if (!string.Equals(normalizedValue, _value, StringComparison.Ordinal))
                {
                    Value = normalizedValue;
                    changed = true;
                }
            }

            if (_title == null)
            {
                Title = string.Empty;
                changed = true;
            }

            if (_source == null)
            {
                Source = string.Empty;
                changed = true;
            }

            return changed;
        }

        internal CopilotAttachmentItem CreateSnapshot()
        {
            return new CopilotAttachmentItem
            {
                Id = Id,
                Type = Type,
                Title = Title,
                Value = Value,
                Source = Source,
                CreatedAt = CreatedAt,
            };
        }

        public static CopilotAttachmentItem CreateFile(string filePath)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.File,
                Title = Path.GetFileName(filePath),
                Value = filePath,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateContext(string text, string? title = null, string? source = null)
        {
            var normalizedText = NormalizeStoredText(text);
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.Context,
                Title = string.IsNullOrWhiteSpace(title) ? BuildPreview(normalizedText, 18) : title,
                Source = source ?? string.Empty,
                Value = normalizedText,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateImage(string imagePath, string? title = null)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.Image,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(imagePath) : title,
                Value = imagePath,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateWebPage(string url, string title, string content)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.WebPage,
                Title = title,
                Source = url,
                Value = NormalizeStoredText(content),
                CreatedAt = DateTime.Now,
            };
        }

        private void ResetPreviewImage()
        {
            lock (_previewSync)
            {
                _previewGeneration++;
                _previewImage = null;
                _previewImagePath = string.Empty;
                _previewLoadingPath = string.Empty;
            }
            OnPropertyChanged(nameof(PreviewImage));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(ImageFallbackText));
        }

        private static string BuildPreview(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
        }

        internal static string NormalizeStoredText(string? value)
        {
            var source = value ?? string.Empty;
            var start = 0;
            while (start < source.Length && char.IsWhiteSpace(source[start]))
                start++;
            var end = source.Length;
            while (end > start && char.IsWhiteSpace(source[end - 1]))
                end--;

            var length = end - start;
            if (length <= MaximumStoredTextCharacters)
                return length == 0 ? string.Empty : source.Substring(start, length);

            var retainedLength = MaximumStoredTextCharacters - StoredTextTruncationMarker.Length;
            if (retainedLength > 0
                && start + retainedLength < end
                && char.IsHighSurrogate(source[start + retainedLength - 1])
                && char.IsLowSurrogate(source[start + retainedLength]))
            {
                retainedLength--;
            }
            return source.Substring(start, retainedLength).TrimEnd() + StoredTextTruncationMarker;
        }

        private static string TryGetHostLabel(string? value)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
                return uri.Host;

            return BuildPreview(value ?? string.Empty, 20);
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    }
}
