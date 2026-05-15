namespace Aoyon.FaceTune.Gui;

internal enum ExpressionManagerThumbnailStatus
{
    Queued,
    Ready,
    Failed
}

internal readonly struct ExpressionManagerThumbnailResult
{
    public Texture2D? Texture { get; }
    public ExpressionManagerThumbnailStatus Status { get; }

    public ExpressionManagerThumbnailResult(Texture2D? texture, ExpressionManagerThumbnailStatus status)
    {
        Texture = texture;
        Status = status;
    }
}

internal sealed class ExpressionManagerThumbnailCache : IDisposable
{
    public const int ThumbnailSize = 144;

    private readonly ExpressionManagerThumbnailRenderer? _renderer;
    private readonly Func<AvatarContext, IReadOnlyBlendShapeSet, int, Texture2D?> _renderThumbnail;
    private readonly Action<Action> _enqueueDelayCall;
    private readonly Dictionary<int, CacheEntry> _entries = new();
    private readonly Queue<QueuedRequest> _queuedRequests = new();
    private bool _isDelayCallScheduled;
    private bool _isDisposed;
    private int _generation;

    public ExpressionManagerThumbnailCache()
    {
        _renderer = new ExpressionManagerThumbnailRenderer();
        _renderThumbnail = _renderer.Render;
        _enqueueDelayCall = action => EditorApplication.delayCall += () => action();
    }

    internal ExpressionManagerThumbnailCache(
        Func<AvatarContext, IReadOnlyBlendShapeSet, int, Texture2D?> renderThumbnail,
        Action<Action> enqueueDelayCall)
    {
        _renderThumbnail = renderThumbnail;
        _enqueueDelayCall = enqueueDelayCall;
    }

    public ExpressionManagerThumbnailResult GetOrRequest(
        ExpressionManagerExpressionItem item,
        AvatarContext avatarContext,
        string rendererState,
        Action repaint)
    {
        var expressionId = item.Expression.GetInstanceID();
        var key = CreateExpressionKey(item, avatarContext, rendererState);
        if (_entries.TryGetValue(expressionId, out var existing) && existing.Key == key)
        {
            return new ExpressionManagerThumbnailResult(existing.Texture, existing.Status);
        }

        if (existing != null)
        {
            DestroyTexture(existing.Texture);
            _entries.Remove(expressionId);
        }

        _entries[expressionId] = new CacheEntry(key, null, ExpressionManagerThumbnailStatus.Queued);
        _queuedRequests.Enqueue(new QueuedRequest(
            expressionId,
            key,
            avatarContext,
            context => ExpressionManagerPreviewBlendShapeCollector.Collect(item.Expression, context),
            repaint,
            _generation));
        ScheduleNext();

        return new ExpressionManagerThumbnailResult(null, ExpressionManagerThumbnailStatus.Queued);
    }

    public ExpressionManagerThumbnailResult GetOrRequest(
        ExpressionManagerUnlinkedSourceItem item,
        AvatarContext avatarContext,
        string rendererState,
        Action repaint)
    {
        var sourceId = item.Component.GetInstanceID();
        var key = CreateUnlinkedSourceKey(item, avatarContext, rendererState);
        if (_entries.TryGetValue(sourceId, out var existing) && existing.Key == key)
        {
            return new ExpressionManagerThumbnailResult(existing.Texture, existing.Status);
        }

        if (existing != null)
        {
            DestroyTexture(existing.Texture);
            _entries.Remove(sourceId);
        }

        _entries[sourceId] = new CacheEntry(key, null, ExpressionManagerThumbnailStatus.Queued);
        _queuedRequests.Enqueue(new QueuedRequest(
            sourceId,
            key,
            avatarContext,
            context => ExpressionManagerPreviewBlendShapeCollector.Collect(item.Component, context),
            repaint,
            _generation));
        ScheduleNext();

        return new ExpressionManagerThumbnailResult(null, ExpressionManagerThumbnailStatus.Queued);
    }

    public void Clear()
    {
        _generation++;
        _queuedRequests.Clear();
        foreach (var entry in _entries.Values)
        {
            DestroyTexture(entry.Texture);
        }
        _entries.Clear();
    }

    public void Dispose()
    {
        _isDisposed = true;
        Clear();
        _renderer?.Dispose();
    }

    public static string CreateRendererState(GameObject avatarRoot)
    {
        return string.Join(
            ";",
            ExpressionManagerThumbnailRenderer
                .CollectRenderableRenderers(avatarRoot)
                .Select(CreateRendererState));
    }

    private void ScheduleNext()
    {
        if (_isDisposed || _isDelayCallScheduled || _queuedRequests.Count == 0) return;

        _isDelayCallScheduled = true;
        _enqueueDelayCall(ProcessNext);
    }

    private void ProcessNext()
    {
        _isDelayCallScheduled = false;
        if (_isDisposed) return;

        while (_queuedRequests.Count > 0)
        {
            var request = _queuedRequests.Dequeue();
            if (request.Generation != _generation) continue;
            if (!_entries.TryGetValue(request.ExpressionId, out var entry)) continue;
            if (entry.Key != request.Key || entry.Status != ExpressionManagerThumbnailStatus.Queued) continue;

            var texture = Render(request);
            _entries[request.ExpressionId] = texture == null
                ? new CacheEntry(request.Key, null, ExpressionManagerThumbnailStatus.Failed)
                : new CacheEntry(request.Key, texture, ExpressionManagerThumbnailStatus.Ready);
            request.Repaint();
            break;
        }

        ScheduleNext();
    }

    private Texture2D? Render(QueuedRequest request)
    {
        var blendShapes = request.CollectBlendShapes(request.AvatarContext);
        return _renderThumbnail(request.AvatarContext, blendShapes, ThumbnailSize);
    }

    private static string CreateExpressionKey(ExpressionManagerExpressionItem item, AvatarContext avatarContext, string rendererState)
    {
        var dirtyCounts = item.EditableTargets
            .Append<Component>(item.Expression)
            .Where(component => component != null)
            .Select(component => EditorUtility.GetDirtyCount(component).ToString());

        return string.Join(
            "|",
            "expression",
            item.Expression.GetInstanceID(),
            avatarContext.FaceMesh.GetInstanceID(),
            rendererState,
            string.Join(",", dirtyCounts));
    }

    private static string CreateUnlinkedSourceKey(ExpressionManagerUnlinkedSourceItem item, AvatarContext avatarContext, string rendererState)
    {
        var dirtyCounts = GetUnlinkedSourceKeyComponents(item)
            .Where(component => component != null)
            .Select(component => EditorUtility.GetDirtyCount(component).ToString());

        return string.Join(
            "|",
            "unlinked-source",
            item.Component.GetInstanceID(),
            avatarContext.FaceMesh.GetInstanceID(),
            rendererState,
            string.Join(",", dirtyCounts));
    }

    private static IEnumerable<Component> GetUnlinkedSourceKeyComponents(ExpressionManagerUnlinkedSourceItem item)
    {
        if (item.Component is ExpressionOverrideComponent expressionOverride)
        {
            foreach (var baseSource in ExpressionDataComponent.GetOverrideBaseSources(expressionOverride, null))
            {
                yield return baseSource;
            }
        }

        foreach (var component in item.Components)
        {
            yield return component;
        }
    }

    private static string CreateRendererState(Renderer renderer)
    {
        var meshId = GetMesh(renderer)?.GetInstanceID() ?? 0;
        var materialIds = string.Join(
            ",",
            renderer.sharedMaterials
                .Where(material => material != null)
                .Select(material => material.GetInstanceID()));

        return string.Join(
            ":",
            renderer.GetInstanceID(),
            EditorUtility.GetDirtyCount(renderer),
            EditorUtility.GetDirtyCount(renderer.transform),
            meshId,
            materialIds);
    }

    private static Mesh? GetMesh(Renderer renderer)
    {
        return renderer switch
        {
            SkinnedMeshRenderer skinnedMeshRenderer => skinnedMeshRenderer.sharedMesh,
            MeshRenderer meshRenderer when meshRenderer.TryGetComponent<MeshFilter>(out var meshFilter) => meshFilter.sharedMesh,
            _ => null
        };
    }

    private static void DestroyTexture(Texture2D? texture)
    {
        if (texture != null)
        {
            Object.DestroyImmediate(texture);
        }
    }

    private sealed class CacheEntry
    {
        public string Key { get; }
        public Texture2D? Texture { get; }
        public ExpressionManagerThumbnailStatus Status { get; }

        public CacheEntry(string key, Texture2D? texture, ExpressionManagerThumbnailStatus status)
        {
            Key = key;
            Texture = texture;
            Status = status;
        }
    }

    private readonly struct QueuedRequest
    {
        public int ExpressionId { get; }
        public string Key { get; }
        public AvatarContext AvatarContext { get; }
        public Func<AvatarContext, IReadOnlyBlendShapeSet> CollectBlendShapes { get; }
        public Action Repaint { get; }
        public int Generation { get; }

        public QueuedRequest(
            int expressionId,
            string key,
            AvatarContext avatarContext,
            Func<AvatarContext, IReadOnlyBlendShapeSet> collectBlendShapes,
            Action repaint,
            int generation)
        {
            ExpressionId = expressionId;
            Key = key;
            AvatarContext = avatarContext;
            CollectBlendShapes = collectBlendShapes;
            Repaint = repaint;
            Generation = generation;
        }
    }
}
