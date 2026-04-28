using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ZwcadAi.Core;

namespace ZwcadAi.Renderer;

public interface IWriterTransactionScope : IDisposable
{
    void Commit();
}

public sealed class WriterRenderOptions
{
    public static readonly WriterRenderOptions Default = new WriterRenderOptions();

    public WriterRenderOptions(
        CancellationToken cancellationToken = default,
        IWriterFailureInjector? failureInjector = null)
    {
        CancellationToken = cancellationToken;
        FailureInjector = failureInjector;
    }

    public CancellationToken CancellationToken { get; }

    public IWriterFailureInjector? FailureInjector { get; }
}

public interface IWriterFailureInjector
{
    void AfterEntityAppended(PlannedEntity plannedEntity, RenderedEntity renderedEntity);

    void AfterDimensionAppended(PlannedDimension plannedDimension, RenderedEntity renderedEntity);

    void BeforeCommit(IReadOnlyList<RenderedEntity> renderedEntities);
}

public sealed class WriterFailureInjection : IWriterFailureInjector
{
    private readonly string? _failAfterSpecEntityId;
    private readonly string? _failAfterSpecDimensionId;
    private readonly bool _failBeforeCommit;

    private WriterFailureInjection(
        string? failAfterSpecEntityId,
        string? failAfterSpecDimensionId,
        bool failBeforeCommit)
    {
        _failAfterSpecEntityId = failAfterSpecEntityId;
        _failAfterSpecDimensionId = failAfterSpecDimensionId;
        _failBeforeCommit = failBeforeCommit;
    }

    public static WriterFailureInjection AfterEntity(string specEntityId)
    {
        if (string.IsNullOrWhiteSpace(specEntityId))
        {
            throw new ArgumentException("Spec entity id is required.", nameof(specEntityId));
        }

        return new WriterFailureInjection(specEntityId, null, false);
    }

    public static WriterFailureInjection AfterDimension(string specDimensionId)
    {
        if (string.IsNullOrWhiteSpace(specDimensionId))
        {
            throw new ArgumentException("Spec dimension id is required.", nameof(specDimensionId));
        }

        return new WriterFailureInjection(null, specDimensionId, false);
    }

    public static WriterFailureInjection BeforeCommit()
    {
        return new WriterFailureInjection(null, null, true);
    }

    public void AfterEntityAppended(PlannedEntity plannedEntity, RenderedEntity renderedEntity)
    {
        if (string.Equals(plannedEntity.SpecEntityId, _failAfterSpecEntityId, StringComparison.Ordinal))
        {
            throw WriterRenderException.ForEntity(
                plannedEntity,
                "Injected failure after entity append.",
                "injected_writer_failure");
        }
    }

    public void AfterDimensionAppended(PlannedDimension plannedDimension, RenderedEntity renderedEntity)
    {
        if (string.Equals(plannedDimension.SpecDimensionId, _failAfterSpecDimensionId, StringComparison.Ordinal))
        {
            throw WriterRenderException.ForDimension(
                plannedDimension,
                "Injected failure after dimension append.",
                "injected_writer_failure");
        }
    }

    public void BeforeCommit(IReadOnlyList<RenderedEntity> renderedEntities)
    {
        if (_failBeforeCommit)
        {
            throw new WriterRenderException(
                "injected_writer_failure",
                "$",
                "Writer failed before committing the transaction.");
        }
    }
}

public sealed class WriterTransactionContext
{
    private readonly WriterRenderOptions _options;

    public WriterTransactionContext(DrawingRenderPlan plan, WriterRenderOptions options)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public DrawingRenderPlan Plan { get; }

    public void ThrowIfCancellationRequested()
    {
        if (_options.CancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(_options.CancellationToken);
        }
    }

    public void AfterEntityAppended(PlannedEntity plannedEntity, RenderedEntity renderedEntity)
    {
        ThrowIfCancellationRequested();
        _options.FailureInjector?.AfterEntityAppended(plannedEntity, renderedEntity);
    }

    public void AfterDimensionAppended(PlannedDimension plannedDimension, RenderedEntity renderedEntity)
    {
        ThrowIfCancellationRequested();
        _options.FailureInjector?.AfterDimensionAppended(plannedDimension, renderedEntity);
    }

    public void BeforeCommit(IReadOnlyList<RenderedEntity> renderedEntities)
    {
        ThrowIfCancellationRequested();
        _options.FailureInjector?.BeforeCommit(renderedEntities);
    }
}

public sealed class WriterTransactionBoundary
{
    private readonly Func<IWriterTransactionScope> _beginTransaction;

    public WriterTransactionBoundary(Func<IWriterTransactionScope> beginTransaction)
    {
        _beginTransaction = beginTransaction ?? throw new ArgumentNullException(nameof(beginTransaction));
    }

    public RenderResult Execute(
        DrawingRenderPlan plan,
        WriterRenderOptions? options,
        Func<IWriterTransactionScope, WriterTransactionContext, IReadOnlyList<RenderedEntity>> write)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        if (write == null)
        {
            throw new ArgumentNullException(nameof(write));
        }

        if (!plan.Validation.IsValid)
        {
            return new RenderResult(RenderStatus.Failed, Array.Empty<RenderedEntity>(), plan.Validation);
        }

        var effectiveOptions = options ?? WriterRenderOptions.Default;
        var context = new WriterTransactionContext(plan, effectiveOptions);

        try
        {
            context.ThrowIfCancellationRequested();

            using (var transaction = _beginTransaction())
            {
                var renderedEntities = (write(transaction, context) ?? Array.Empty<RenderedEntity>()).ToArray();
                context.BeforeCommit(renderedEntities);
                transaction.Commit();

                return new RenderResult(RenderStatus.Success, renderedEntities, ValidationResult.Success());
            }
        }
        catch (OperationCanceledException)
        {
            return new RenderResult(
                RenderStatus.Canceled,
                Array.Empty<RenderedEntity>(),
                ValidationResult.Failure(new[]
                {
                    new ValidationIssue(
                        "render_canceled",
                        "$",
                        "Render was canceled before the writer transaction committed.",
                        ValidationSeverity.Warning)
                }));
        }
        catch (WriterRenderException exception)
        {
            return new RenderResult(
                RenderStatus.Failed,
                Array.Empty<RenderedEntity>(),
                ValidationResult.Failure(new[]
                {
                    new ValidationIssue(exception.Code, exception.Path, exception.Message, ValidationSeverity.Error)
                }));
        }
        catch (Exception exception)
        {
            return new RenderResult(
                RenderStatus.Failed,
                Array.Empty<RenderedEntity>(),
                ValidationResult.Failure(new[]
                {
                    new ValidationIssue(
                        "writer_transaction_failed",
                        "$",
                        exception.Message,
                        ValidationSeverity.Error)
                }));
        }
    }
}

public sealed class WriterRenderException : InvalidOperationException
{
    public WriterRenderException(string code, string path, string message)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Writer render failure code is required.", nameof(code));
        }

        Code = code;
        Path = path ?? string.Empty;
    }

    public string Code { get; }

    public string Path { get; }

    public static WriterRenderException ForEntity(
        PlannedEntity plannedEntity,
        string message,
        string code = "entity_render_failed")
    {
        if (plannedEntity == null)
        {
            throw new ArgumentNullException(nameof(plannedEntity));
        }

        var path = $"$.entities[{plannedEntity.SpecEntityId}]";
        return new WriterRenderException(code, path, $"Entity '{path}' failed to render: {message}");
    }

    public static WriterRenderException ForDimension(
        PlannedDimension plannedDimension,
        string message,
        string code = "dimension_render_failed")
    {
        if (plannedDimension == null)
        {
            throw new ArgumentNullException(nameof(plannedDimension));
        }

        var path = $"$.dimensions[{plannedDimension.SpecDimensionId}]";
        return new WriterRenderException(code, path, $"Dimension '{path}' failed to render: {message}");
    }
}
