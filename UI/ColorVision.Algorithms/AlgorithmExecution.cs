using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace ColorVision.Algorithms;

public sealed record AlgorithmProgress(double Fraction, string Stage, string? Message = null);

public sealed class AlgorithmExecutionContext
{
    public required AlgorithmDescriptor Descriptor { get; init; }

    public required AlgorithmInvocation Invocation { get; init; }

    public required IAlgorithmParameters Parameters { get; init; }

    public required IReadOnlyList<AlgorithmInput> Inputs { get; init; }

    public IProgress<AlgorithmProgress>? Progress { get; init; }
}

public interface IImageAlgorithmProvider
{
    AlgorithmProviderMetadata Metadata { get; }

    bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason);

    ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken);
}

public interface IAlgorithmExecutionScheduler
{
    ValueTask<AlgorithmResult> ScheduleAsync(
        AlgorithmProviderMetadata metadata,
        Func<CancellationToken, ValueTask<AlgorithmResult>> operation,
        CancellationToken cancellationToken);
}

public sealed class AlgorithmExecutionScheduler : IAlgorithmExecutionScheduler, IDisposable
{
    private readonly IReadOnlyDictionary<AlgorithmProviderKind, SemaphoreSlim> _gates;

    public AlgorithmExecutionScheduler(int cpuConcurrency = 0, int nativeConcurrency = 2, int gpuConcurrency = 1, int remoteConcurrency = 4)
    {
        cpuConcurrency = cpuConcurrency <= 0 ? Math.Max(1, Environment.ProcessorCount - 1) : cpuConcurrency;
        _gates = new Dictionary<AlgorithmProviderKind, SemaphoreSlim>
        {
            [AlgorithmProviderKind.Cpu] = new(cpuConcurrency, cpuConcurrency),
            [AlgorithmProviderKind.Native] = new(Math.Max(1, nativeConcurrency), Math.Max(1, nativeConcurrency)),
            [AlgorithmProviderKind.Gpu] = new(Math.Max(1, gpuConcurrency), Math.Max(1, gpuConcurrency)),
            [AlgorithmProviderKind.Remote] = new(Math.Max(1, remoteConcurrency), Math.Max(1, remoteConcurrency)),
        };
    }

    public async ValueTask<AlgorithmResult> ScheduleAsync(
        AlgorithmProviderMetadata metadata,
        Func<CancellationToken, ValueTask<AlgorithmResult>> operation,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = _gates[metadata.Kind];
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AlgorithmResult result;
            if (metadata.Kind == AlgorithmProviderKind.Remote)
                result = await operation(cancellationToken).ConfigureAwait(false);
            else
                result = await Task.Run(async () => await operation(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                result.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        foreach (SemaphoreSlim gate in _gates.Values) gate.Dispose();
    }
}

public sealed class AlgorithmRunRequest
{
    public required AlgorithmInvocation Invocation { get; init; }

    public IReadOnlyList<AlgorithmInput> Inputs { get; init; } = Array.Empty<AlgorithmInput>();

    public AlgorithmHostCapabilities RequiredCapabilities { get; init; } = AlgorithmHostCapabilities.None;

    public string? PreferredProviderId { get; init; }

    public IProgress<AlgorithmProgress>? Progress { get; init; }
}

public sealed class AlgorithmRunner
{
    private readonly IAlgorithmCatalog _catalog;
    private readonly IReadOnlyList<IImageAlgorithmProvider> _providers;
    private readonly IAlgorithmExecutionScheduler _scheduler;
    private readonly Dictionary<(AlgorithmId Id, int From), IAlgorithmParameterMigrator> _migrators;

    public AlgorithmRunner(
        IAlgorithmCatalog catalog,
        IEnumerable<IImageAlgorithmProvider> providers,
        IAlgorithmExecutionScheduler scheduler,
        IEnumerable<IAlgorithmParameterMigrator>? migrators = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _providers = providers?.OrderByDescending(provider => provider.Metadata.Priority).ToArray()
            ?? throw new ArgumentNullException(nameof(providers));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _migrators = (migrators ?? Array.Empty<IAlgorithmParameterMigrator>())
            .ToDictionary(migrator => (migrator.AlgorithmId, migrator.FromSchemaVersion));
    }

    public async ValueTask<AlgorithmResult> RunAsync(AlgorithmRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Stopwatch stopwatch = Stopwatch.StartNew();
        AlgorithmDescriptor? descriptor = null;
        AlgorithmProviderMetadata? providerMetadata = null;
        try
        {
            if (!_catalog.TryResolve(request.Invocation.AlgorithmId, out descriptor) || descriptor == null)
                return Failure(request.Invocation, descriptor, "algorithm_not_found", $"Algorithm '{request.Invocation.AlgorithmId}' is not registered.", startedAt, stopwatch.Elapsed);

            if (request.Invocation.AlgorithmVersion is AlgorithmVersion requestedVersion && requestedVersion.Major != descriptor.Version.Major)
                return Failure(request.Invocation, descriptor, "algorithm_version_incompatible", $"Requested {requestedVersion}; catalog provides {descriptor.Version}.", startedAt, stopwatch.Elapsed);

            if ((descriptor.Capabilities & request.RequiredCapabilities) != request.RequiredCapabilities)
                return Failure(request.Invocation, descriptor, "host_capability_unsupported", "The algorithm does not declare all capabilities required by this host.", startedAt, stopwatch.Elapsed);

            if (request.Inputs.Count < descriptor.MinimumInputCount || request.Inputs.Count > descriptor.MaximumInputCount)
                return Failure(request.Invocation, descriptor, "invalid_input_count", $"Expected {descriptor.MinimumInputCount}..{descriptor.MaximumInputCount} inputs; received {request.Inputs.Count}.", startedAt, stopwatch.Elapsed);

            foreach (AlgorithmInput input in request.Inputs)
            {
                if (!descriptor.SupportedFormats.Contains(input.Image.Format))
                    return Failure(request.Invocation, descriptor, "unsupported_format", $"Input '{input.Name}' format {input.Image.Format} is unsupported.", startedAt, stopwatch.Elapsed, "inputs");
            }

            AlgorithmValidationResult? roiValidation = ValidateRoi(descriptor, request.Invocation.Roi);
            if (roiValidation is { IsValid: false })
                return ValidationFailure(request.Invocation, descriptor, roiValidation, startedAt, stopwatch.Elapsed);

            JsonElement parameterJson = MigrateParameters(request.Invocation, descriptor);
            if (parameterJson.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) parameterJson = descriptor.ParameterSchema.Defaults;
            IAlgorithmParameters parameters;
            try
            {
                parameters = parameterJson.Deserialize(descriptor.ParameterType, AlgorithmJson.Options) as IAlgorithmParameters
                    ?? throw new AlgorithmRequestException("invalid_parameters", $"Parameters do not implement {nameof(IAlgorithmParameters)}.");
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                throw new AlgorithmRequestException("invalid_parameters", exception.Message, exception);
            }
            if (parameters.SchemaVersion != descriptor.ParameterSchema.Version)
                throw new AlgorithmRequestException("parameter_schema_mismatch", $"Deserialized parameters report schema {parameters.SchemaVersion}; expected {descriptor.ParameterSchema.Version}.");

            AlgorithmValidationResult parameterValidation = parameters.Validate();
            if (!parameterValidation.IsValid)
                return ValidationFailure(request.Invocation, descriptor, parameterValidation, startedAt, stopwatch.Elapsed);

            IImageAlgorithmProvider? provider = SelectProvider(request, descriptor, out IReadOnlyList<AlgorithmDiagnosticMessage> selectionDiagnostics);
            if (provider == null)
                return Failure(request.Invocation, descriptor, "provider_unavailable", "No compatible provider is available.", startedAt, stopwatch.Elapsed,
                    details: selectionDiagnostics.GroupBy(message => message.Code).ToDictionary(group => group.Key, group => string.Join(" | ", group.Select(message => message.Message))));

            providerMetadata = provider.Metadata;
            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(request.Progress, new AlgorithmProgress(0, "queued"));
            AlgorithmResult result = await _scheduler.ScheduleAsync(provider.Metadata, async token =>
            {
                ReportProgress(request.Progress, new AlgorithmProgress(0.01, "executing", provider.Metadata.Name));
                AlgorithmResult providerResult = await provider.ExecuteAsync(new AlgorithmExecutionContext
                {
                    Descriptor = descriptor,
                    Invocation = request.Invocation,
                    Parameters = parameters,
                    Inputs = request.Inputs,
                    Progress = request.Progress,
                }, token).ConfigureAwait(false);
                return providerResult;
            }, cancellationToken).ConfigureAwait(false);
            AlgorithmImageArtifact? invalidOutput = result.Artifacts
                .OfType<AlgorithmImageArtifact>()
                .FirstOrDefault(artifact => descriptor.OutputFormats != null && !descriptor.OutputFormats.Contains(artifact.Image.Format));
            if (invalidOutput != null)
            {
                string actualFormat = invalidOutput.Image.Format.ToString();
                result.Dispose();
                result = new AlgorithmResult
                {
                    InvocationId = request.Invocation.InvocationId,
                    AlgorithmId = descriptor.Id,
                    AlgorithmVersion = descriptor.Version,
                    Status = AlgorithmResultStatus.Failed,
                    Failures = new[]
                    {
                        new AlgorithmFailure("provider_output_format_violation", $"Provider returned {actualFormat}, outside the descriptor output contract."),
                    },
                };
            }
            AlgorithmResult normalized = NormalizeResult(result, request.Invocation, descriptor, provider.Metadata, startedAt, stopwatch.Elapsed, selectionDiagnostics);
            ReportProgress(request.Progress, new AlgorithmProgress(1, normalized.Status switch
            {
                AlgorithmResultStatus.Succeeded => "completed",
                AlgorithmResultStatus.Cancelled => "cancelled",
                AlgorithmResultStatus.Superseded => "superseded",
                _ => "failed",
            }));
            return normalized;
        }
        catch (OperationCanceledException)
        {
            ReportProgress(request.Progress, new AlgorithmProgress(1, "cancelled"));
            return new AlgorithmResult
            {
                InvocationId = request.Invocation.InvocationId,
                AlgorithmId = request.Invocation.AlgorithmId,
                AlgorithmVersion = descriptor?.Version ?? default,
                Status = AlgorithmResultStatus.Cancelled,
                Failures = new[] { new AlgorithmFailure("cancelled", "Algorithm execution was cancelled.") },
                Diagnostics = new AlgorithmExecutionDiagnostics
                {
                    ProviderId = providerMetadata?.ProviderId,
                    ProviderKind = providerMetadata?.Kind,
                    StartedAt = startedAt,
                    Duration = stopwatch.Elapsed,
                },
            };
        }
        catch (AlgorithmRequestException exception)
        {
            ReportProgress(request.Progress, new AlgorithmProgress(1, "failed", exception.Code));
            return Failure(request.Invocation, descriptor, exception.Code, exception.Message, startedAt, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            ReportProgress(request.Progress, new AlgorithmProgress(1, "failed", exception.GetType().Name));
            return Failure(request.Invocation, descriptor, "execution_exception", exception.Message, startedAt, stopwatch.Elapsed,
                details: new Dictionary<string, string> { ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name });
        }
        finally
        {
            foreach (AlgorithmInput input in request.Inputs.Where(input => input.Ownership == AlgorithmInputOwnership.Transferred)) input.Image.Dispose();
        }
    }

    private JsonElement MigrateParameters(AlgorithmInvocation invocation, AlgorithmDescriptor descriptor)
    {
        JsonElement parameters = invocation.HasParameters ? invocation.Parameters : descriptor.ParameterSchema.Defaults;
        int version = invocation.HasParameters ? invocation.ParameterSchemaVersion : descriptor.ParameterSchema.Version;
        if (version <= 0)
            throw new AlgorithmRequestException("parameter_schema_invalid", $"Parameter schema {version} is invalid.");
        if (version > descriptor.ParameterSchema.Version)
            throw new AlgorithmRequestException("parameter_schema_newer", $"Parameter schema {version} is newer than supported schema {descriptor.ParameterSchema.Version}.");

        while (version < descriptor.ParameterSchema.Version)
        {
            if (!_migrators.TryGetValue((descriptor.Id, version), out IAlgorithmParameterMigrator? migrator))
                throw new AlgorithmRequestException("parameter_migration_missing", $"No parameter migration exists for '{descriptor.Id}' schema {version}.");
            if (migrator.ToSchemaVersion <= version)
                throw new AlgorithmRequestException("parameter_migration_invalid", $"Parameter migrator for '{descriptor.Id}' schema {version} does not advance the schema.");
            if (migrator.ToSchemaVersion > descriptor.ParameterSchema.Version)
                throw new AlgorithmRequestException("parameter_migration_invalid", $"Parameter migrator for '{descriptor.Id}' advances beyond supported schema {descriptor.ParameterSchema.Version}.");
            parameters = migrator.Migrate(parameters);
            version = migrator.ToSchemaVersion;
        }

        return parameters;
    }

    private static void ReportProgress(IProgress<AlgorithmProgress>? progress, AlgorithmProgress value)
    {
        try
        {
            progress?.Report(value);
        }
        catch
        {
            // Progress observers are informational and must not change execution or artifact ownership.
        }
    }

    private sealed class AlgorithmRequestException : Exception
    {
        public AlgorithmRequestException(string code, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Code = code;
        }

        public string Code { get; }
    }

    private IImageAlgorithmProvider? SelectProvider(
        AlgorithmRunRequest request,
        AlgorithmDescriptor descriptor,
        out IReadOnlyList<AlgorithmDiagnosticMessage> diagnostics)
    {
        List<AlgorithmDiagnosticMessage> messages = new();
        IEnumerable<IImageAlgorithmProvider> candidates = _providers;
        if (!string.IsNullOrWhiteSpace(request.PreferredProviderId))
            candidates = candidates.OrderByDescending(provider => string.Equals(provider.Metadata.ProviderId, request.PreferredProviderId, StringComparison.OrdinalIgnoreCase));

        foreach (IImageAlgorithmProvider provider in candidates)
        {
            if ((provider.Metadata.Capabilities & request.RequiredCapabilities) != request.RequiredCapabilities)
            {
                messages.Add(new AlgorithmDiagnosticMessage("provider_capability_mismatch", $"{provider.Metadata.ProviderId} lacks required host capabilities.", "debug"));
                continue;
            }
            if (request.Inputs.Any(input => !provider.Metadata.SupportedFormats.Contains(input.Image.Format)))
            {
                messages.Add(new AlgorithmDiagnosticMessage("provider_format_mismatch", $"{provider.Metadata.ProviderId} does not declare every requested input format.", "debug"));
                continue;
            }
            if (provider.CanExecute(descriptor, request.Inputs, out string? reason))
            {
                diagnostics = messages;
                return provider;
            }
            messages.Add(new AlgorithmDiagnosticMessage("provider_rejected", $"{provider.Metadata.ProviderId}: {reason}", "debug"));
        }

        diagnostics = messages;
        return null;
    }

    private static AlgorithmValidationResult? ValidateRoi(AlgorithmDescriptor descriptor, AlgorithmRoi? roi)
    {
        if (roi == null) return null;
        bool supported = roi switch
        {
            RectangleAlgorithmRoi => descriptor.SupportsRectangleRoi,
            CircleAlgorithmRoi => descriptor.SupportsCircleRoi,
            PolygonAlgorithmRoi => descriptor.SupportsPolygonRoi,
            PolylineAlgorithmRoi => descriptor.SupportsPolylineRoi,
            _ => false,
        };
        AlgorithmValidationResult validation = roi.Validate();
        if (!supported) validation.Add("roi", "roi_kind_unsupported", $"{roi.GetType().Name} is unsupported by '{descriptor.Id}'.");
        return validation;
    }

    private static AlgorithmResult NormalizeResult(
        AlgorithmResult result,
        AlgorithmInvocation invocation,
        AlgorithmDescriptor descriptor,
        AlgorithmProviderMetadata provider,
        DateTimeOffset startedAt,
        TimeSpan duration,
        IReadOnlyList<AlgorithmDiagnosticMessage> selectionDiagnostics)
    {
        return new AlgorithmResult
        {
            InvocationId = invocation.InvocationId,
            AlgorithmId = descriptor.Id,
            AlgorithmVersion = descriptor.Version,
            Status = result.Status,
            Artifacts = result.Artifacts,
            Failures = result.Failures,
            Diagnostics = new AlgorithmExecutionDiagnostics
            {
                ProviderId = provider.ProviderId,
                ProviderKind = provider.Kind,
                StartedAt = startedAt,
                Duration = duration,
                Messages = selectionDiagnostics.Concat(result.Diagnostics.Messages).ToArray(),
            },
        };
    }

    private static AlgorithmResult ValidationFailure(
        AlgorithmInvocation invocation,
        AlgorithmDescriptor descriptor,
        AlgorithmValidationResult validation,
        DateTimeOffset startedAt,
        TimeSpan duration)
        => new()
        {
            InvocationId = invocation.InvocationId,
            AlgorithmId = descriptor.Id,
            AlgorithmVersion = descriptor.Version,
            Status = AlgorithmResultStatus.Failed,
            Failures = validation.Issues.Select(issue => new AlgorithmFailure(issue.Code, issue.Message, issue.Path)).ToArray(),
            Diagnostics = new AlgorithmExecutionDiagnostics { StartedAt = startedAt, Duration = duration },
        };

    private static AlgorithmResult Failure(
        AlgorithmInvocation invocation,
        AlgorithmDescriptor? descriptor,
        string code,
        string message,
        DateTimeOffset startedAt,
        TimeSpan duration,
        string? path = null,
        IReadOnlyDictionary<string, string>? details = null)
        => new()
        {
            InvocationId = invocation.InvocationId,
            AlgorithmId = invocation.AlgorithmId,
            AlgorithmVersion = descriptor?.Version ?? default,
            Status = AlgorithmResultStatus.Failed,
            Failures = new[] { new AlgorithmFailure(code, message, path, details) },
            Diagnostics = new AlgorithmExecutionDiagnostics { StartedAt = startedAt, Duration = duration },
        };
}
