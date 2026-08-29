using ColorVision.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.GeometricTransform;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageRegistration;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.LensDistortionCorrection;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImagingCorrection;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FrequencySpectrum;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.MoireAnalysis;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// WPF compatibility adapter over the catalog-driven interactive presentation projection.
    /// Specialized editors remain mapped here, but membership, capability filtering and order
    /// are owned by the catalog rather than by a second UI algorithm list.
    /// </summary>
    public record class AlgorithmsContextMenu(ImageProcessingContext imageContext) : IIEditorToolContextMenu
    {
        private readonly DrawEditorContext? _drawContext;
        private readonly IAlgorithmAnalysisResultPresenter _analysisPresenter = DefaultAlgorithmAnalysisResultPresenter.Instance;
        private readonly Func<AlgorithmRuntime, BatchImageProcessingWindow> _batchWindowFactory = runtime => new BatchImageProcessingWindow(runtime);
        private readonly Action<BatchImageProcessingWindow> _batchWindowPresenter = ShowBatchWindowDefault;

        private AlgorithmRuntime Runtime => (imageContext ?? throw new InvalidOperationException("The menu has no image context.")).AlgorithmRuntime;

        public AlgorithmsContextMenu(ImageProcessingContext imageContext, DrawEditorContext drawContext)
            : this(imageContext)
        {
            _drawContext = drawContext ?? throw new ArgumentNullException(nameof(drawContext));
        }

        internal AlgorithmsContextMenu(
            ImageProcessingContext imageContext,
            AlgorithmRuntime runtime,
            Action<AlgorithmResult, string>? analysisPresenter = null)
            : this(
                imageContext,
                runtime,
                analysisPresenter == null ? DefaultAlgorithmAnalysisResultPresenter.Instance : new DelegateAlgorithmAnalysisResultPresenter(analysisPresenter),
                runtime => new BatchImageProcessingWindow(runtime),
                ShowBatchWindowDefault)
        {
        }

        internal AlgorithmsContextMenu(
            ImageProcessingContext imageContext,
            AlgorithmRuntime runtime,
            IAlgorithmAnalysisResultPresenter analysisPresenter,
            Func<AlgorithmRuntime, BatchImageProcessingWindow> batchWindowFactory,
            Action<BatchImageProcessingWindow> batchWindowPresenter)
            : this(imageContext)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(analysisPresenter);
            ArgumentNullException.ThrowIfNull(batchWindowFactory);
            ArgumentNullException.ThrowIfNull(batchWindowPresenter);
            if (!ReferenceEquals(runtime, imageContext.AlgorithmRuntime))
                throw new ArgumentException("The menu runtime must be the ImageProcessingContext runtime.", nameof(runtime));
            _analysisPresenter = analysisPresenter;
            _batchWindowFactory = batchWindowFactory;
            _batchWindowPresenter = batchWindowPresenter;
        }

        public List<MenuItemMetadata> GetContextMenuItems()
        {
            List<MenuItemMetadata> items =
            [
                new()
                {
                    GuidId = "Algorithms",
                    Order = 103,
                    Header = ColorVision.ImageEditor.Properties.Resources.ImageAlgorithm,
                },
                new()
                {
                    GuidId = "AlgorithmsCall",
                    Order = 104,
                    Header = ColorVision.ImageEditor.Properties.Resources.Algorithm_AlgorithmCalls,
                },
                new()
                {
                    OwnerGuid = "Algorithms",
                    GuidId = "BatchImageProcessing",
                    Order = 0,
                    Header = "批量执行算法...",
                    Command = new RelayCommand(_ => ShowBatchWindow()),
                },
                new()
                {
                    OwnerGuid = "AlgorithmsCall",
                    GuidId = "SFR",
                    Order = 1,
                    Header = ColorVision.ImageEditor.Properties.Resources.Algorithm_SfrMtfAnalysis,
                    Command = new RelayCommand(_ => new SFREditorTool(imageContext).Execute()),
                },
                new()
                {
                    OwnerGuid = "AlgorithmsCall",
                    GuidId = "Artculation",
                    Order = 1,
                    Header = ColorVision.ImageEditor.Properties.Resources.Artculation_MenuHeader,
                    Command = new RelayCommand(_ => new ArtculationEditorTool(imageContext).Execute()),
                },
            ];

            AlgorithmInteractiveCatalogEntry[] interactiveEntries = AlgorithmCatalogProjection.ForInteractiveMenu(Runtime.Catalog)
                .Where(entry => CanExecuteDescriptor(entry.Descriptor))
                .ToArray();
            foreach (AlgorithmInteractiveGroupPresentation group in interactiveEntries
                .Select(entry => entry.Presentation.Group)
                .OfType<AlgorithmInteractiveGroupPresentation>()
                .DistinctBy(group => group.Id, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Order)
                .ThenBy(group => group.Id, StringComparer.Ordinal))
            {
                items.Add(new MenuItemMetadata
                {
                    OwnerGuid = "Algorithms",
                    GuidId = group.Id,
                    Order = group.Order,
                    Header = ResolveHeader(group.DisplayName, group.ResourceKey, group.Id),
                });
            }

            Dictionary<string, int> groupItemOrders = new(StringComparer.OrdinalIgnoreCase);
            foreach (AlgorithmInteractiveCatalogEntry entry in interactiveEntries)
            {
                string ownerGuid = entry.Presentation.Group?.Id ?? "Algorithms";
                int order = entry.Presentation.Order;
                if (entry.Presentation.Group != null)
                {
                    groupItemOrders.TryGetValue(ownerGuid, out int previousOrder);
                    order = previousOrder + 1;
                    groupItemOrders[ownerGuid] = order;
                }
                items.Add(new MenuItemMetadata
                {
                    OwnerGuid = ownerGuid,
                    GuidId = entry.Presentation.CompatibilityId,
                    Order = order,
                    Header = ResolveHeader(entry),
                    Command = CreateCommand(entry),
                });
            }

            return items;
        }

        private void ShowBatchWindow()
        {
            BatchImageProcessingWindow window = _batchWindowFactory(Runtime);
            _batchWindowPresenter(window);
        }

        private static void ShowBatchWindowDefault(BatchImageProcessingWindow window)
        {
            window.Owner = Application.Current.GetActiveWindow();
            window.ShowDialog();
        }

        private RelayCommand CreateCommand(AlgorithmInteractiveCatalogEntry entry)
        {
            string compatibilityId = entry.Presentation.CompatibilityId;
            AlgorithmId id = entry.Descriptor.Id;
            if (!UsesSpecializedAdapter(entry.Descriptor))
                return new RelayCommand(
                    _ => _ = ExecuteCatalogDefaultAsync(entry.Descriptor),
                    _ => CanExecuteDescriptor(entry.Descriptor));
            RelayCommand specialized = (id, compatibilityId) switch
            {
                (var algorithmId, "InvertImage") when algorithmId == StandardAlgorithmIds.Invert => new RelayCommand(_ => new InvertEditorTool(imageContext).Execute()),
                (var algorithmId, "AutoLevelsAdjust") when algorithmId == StandardAlgorithmIds.AutoLevels => new RelayCommand(_ => new AutoLevelsAdjustEditorTool(imageContext).Execute()),
                (var algorithmId, "WhiteBalance") when algorithmId == StandardAlgorithmIds.WhiteBalance => new RelayCommand(_ => ShowWindow(new WhiteBalanceWindow(imageContext)), _ => IsColorImage()),
                (var algorithmId, "BasicAdjustment") when algorithmId == StandardAlgorithmIds.BasicAdjustment => new RelayCommand(_ => ShowWindow(new BasicAdjustmentWindow(imageContext))),
                (var algorithmId, "Threshold") when algorithmId == StandardAlgorithmIds.Threshold => new RelayCommand(_ => ShowWindow(new ThresholdWindow(imageContext))),
                (var algorithmId, "RemoveMoire") when algorithmId == StandardAlgorithmIds.RemoveMoire => new RelayCommand(_ => new RemoveMoireEditorTool(imageContext).Execute()),
                (var algorithmId, "Sharpen") when algorithmId == StandardAlgorithmIds.Sharpen => new RelayCommand(_ => new SharpenEditorTool(imageContext).Execute()),
                (var algorithmId, "GaussianBlur") when algorithmId == StandardAlgorithmIds.GaussianBlur => new RelayCommand(_ => ShowWindow(new GaussianBlurWindow(imageContext))),
                (var algorithmId, "MedianBlur") when algorithmId == StandardAlgorithmIds.MedianBlur => new RelayCommand(_ => ShowWindow(new MedianBlurWindow(imageContext))),
                (var algorithmId, "EdgeDetection") when algorithmId == StandardAlgorithmIds.Canny => new RelayCommand(_ => ShowWindow(new EdgeDetectionWindow(imageContext))),
                (var algorithmId, "HistogramEqualization") when algorithmId == StandardAlgorithmIds.HistogramEqualization => new RelayCommand(_ => new HistogramEqualizationEditorTool(imageContext).Execute()),
                (var algorithmId, "Erode") when algorithmId == StandardAlgorithmIds.Morphology => new RelayCommand(_ => ShowWindow(new MorphologyWindow(imageContext, 0))),
                (var algorithmId, "Dilate") when algorithmId == StandardAlgorithmIds.Morphology => new RelayCommand(_ => ShowWindow(new MorphologyWindow(imageContext, 1))),
                (var algorithmId, "MorphologyEx") when algorithmId == StandardAlgorithmIds.Morphology => new RelayCommand(_ => ShowWindow(new MorphologyWindow(imageContext, 2))),
                (var algorithmId, "BilateralFilter") when algorithmId == StandardAlgorithmIds.Denoise => new RelayCommand(_ => ShowWindow(new FilterDenoiseWindow(imageContext, 0))),
                (var algorithmId, "Blur") when algorithmId == StandardAlgorithmIds.Denoise => new RelayCommand(_ => ShowWindow(new FilterDenoiseWindow(imageContext, 1))),
                (var algorithmId, "GeometricTransform") when algorithmId == StandardAlgorithmIds.GeometricTransform => new RelayCommand(_ => _ = new GeometricTransformEditorTool(imageContext).ExecuteAsync()),
                (var algorithmId, "ImageRegistration") when algorithmId == StandardAlgorithmIds.ImageRegistration => new RelayCommand(_ => _ = new ImageRegistrationEditorTool(imageContext, _drawContext).ExecuteAsync()),
                (var algorithmId, "LensDistortionCorrection") when algorithmId == StandardAlgorithmIds.LensDistortionCorrection => new RelayCommand(_ => _ = new LensDistortionCorrectionEditorTool(imageContext).ExecuteAsync()),
                (var algorithmId, "ImagingCorrection") when algorithmId == StandardAlgorithmIds.ImagingCorrection => new RelayCommand(_ => _ = new ImagingCorrectionEditorTool(imageContext).ExecuteAsync()),
                (var algorithmId, "FrequencySpectrum") when algorithmId == StandardAlgorithmIds.FrequencySpectrum => new RelayCommand(_ => _ = new FrequencySpectrumEditorTool(imageContext).ExecuteAsync()),
                (var algorithmId, "MoireAnalysis") when algorithmId == StandardAlgorithmIds.MoireAnalysis => new RelayCommand(_ => _ = new MoireAnalysisEditorTool(imageContext).ExecuteAsync()),
                _ => new RelayCommand(
                    _ => _ = ExecuteCatalogDefaultAsync(entry.Descriptor),
                    _ => CanExecuteDescriptor(entry.Descriptor)),
            };
            return new RelayCommand(
                parameter => specialized.Execute(parameter),
                parameter => CanExecuteDescriptor(entry.Descriptor) && specialized.CanExecute(parameter));
        }

        private bool CanExecuteDescriptor(AlgorithmDescriptor descriptor)
        {
            int plannedInputCount = UsesSpecializedAdapter(descriptor)
                && descriptor.Id == StandardAlgorithmIds.ImageRegistration
                ? 2
                : 1;
            return StandardAlgorithmAdapterContract.TryGetInteractiveRequiredCapabilities(
                    descriptor,
                    plannedInputCount,
                    hasRoi: false,
                    AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                    out AlgorithmHostCapabilities required)
                && Runtime.CanExecuteDescriptor(descriptor, required);
        }

        internal static bool UsesSpecializedAdapter(AlgorithmDescriptor descriptor)
            => StandardAlgorithmAdapterContract.IsCompatible(descriptor);

        private static string ResolveHeader(AlgorithmInteractiveCatalogEntry entry)
            => ResolveHeader(entry.Presentation.DisplayName, entry.Presentation.ResourceKey, entry.Descriptor.Name);

        private static string ResolveHeader(string? displayName, string? resourceKey, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(resourceKey))
            {
                CultureInfo culture = ColorVision.ImageEditor.Properties.Resources.Culture ?? CultureInfo.CurrentUICulture;
                string? localized = ColorVision.ImageEditor.Properties.Resources.ResourceManager.GetString(resourceKey, culture);
                if (!string.IsNullOrWhiteSpace(localized)) return localized;
            }
            return displayName ?? fallback;
        }

        private bool IsColorImage() => imageContext.Config.GetProperties<int>("Channel") > 1;

        private static void ShowWindow(Window window)
        {
            window.Owner = Application.Current.GetActiveWindow();
            window.ShowDialog();
        }

        internal async Task<AlgorithmResultStatus> ExecuteCatalogDefaultAsync(AlgorithmDescriptor descriptor)
        {
            try
            {
                IAlgorithmParameters parameters = descriptor.ParameterSchema.Defaults
                    .Deserialize(descriptor.ParameterType, AlgorithmJson.Options) as IAlgorithmParameters
                    ?? throw new InvalidOperationException($"Could not create default parameters for '{descriptor.Id}'.");
                if (parameters is not NoAlgorithmParameters)
                {
                    bool submitted = false;
                    PropertyEditorWindow editor = new(parameters, PropertyEditorEditMode.Transactional)
                    {
                        Owner = Application.Current.GetActiveWindow(),
                        Title = descriptor.Name,
                    };
                    editor.Submitted += (_, _) => submitted = true;
                    editor.ShowDialog();
                    if (!submitted) return AlgorithmResultStatus.Cancelled;
                }

                AlgorithmValidationResult validation = parameters.Validate();
                if (!validation.IsValid)
                {
                    MessageBox.Show(string.Join("; ", validation.Issues), descriptor.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return AlgorithmResultStatus.Failed;
                }

                AlgorithmInvocation invocation = AlgorithmInvocation.Create(descriptor.Id, parameters);
                using AlgorithmResult result = await ImageAlgorithmApplier.ApplyAsync(imageContext, invocation);
                if (result.Status == AlgorithmResultStatus.Succeeded
                    && descriptor.ResultSemantics == AlgorithmResultSemantics.Analysis)
                {
                    _analysisPresenter.Present(result, descriptor.Name);
                }
                if (result.Status is not AlgorithmResultStatus.Succeeded and not AlgorithmResultStatus.Cancelled)
                {
                    MessageBox.Show(string.Join("; ", result.Failures), descriptor.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return result.Status;
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, descriptor.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                return AlgorithmResultStatus.Failed;
            }
        }

    }
}
