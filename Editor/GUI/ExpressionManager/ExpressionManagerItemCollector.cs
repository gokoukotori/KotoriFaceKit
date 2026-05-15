namespace Aoyon.FaceTune.Gui;

internal sealed class ExpressionManagerExpressionItem
{
    public GameObject AvatarRoot { get; }
    public ExpressionComponent Expression { get; }
    public string HierarchyPath { get; }
    public IReadOnlyList<ExpressionDataComponent> ExpressionDataComponents { get; }
    public IReadOnlyList<Component> EditableTargets { get; }
    public int ConditionCount { get; }

    public ExpressionManagerExpressionItem(
        GameObject avatarRoot,
        ExpressionComponent expression,
        string hierarchyPath,
        IReadOnlyList<ExpressionDataComponent> expressionDataComponents,
        IReadOnlyList<Component> editableTargets,
        int conditionCount)
    {
        AvatarRoot = avatarRoot;
        Expression = expression;
        HierarchyPath = hierarchyPath;
        ExpressionDataComponents = expressionDataComponents;
        EditableTargets = editableTargets;
        ConditionCount = conditionCount;
    }
}

internal sealed class ExpressionManagerUnlinkedSourceItem
{
    public ExpressionDataSourceComponent Component { get; }
    public IReadOnlyList<ExpressionDataSourceComponent> Components { get; }
    public string HierarchyPath { get; }

    public ExpressionManagerUnlinkedSourceItem(ExpressionDataSourceComponent component, string hierarchyPath)
        : this(new[] { component }, hierarchyPath)
    {
    }

    public ExpressionManagerUnlinkedSourceItem(
        IReadOnlyList<ExpressionDataSourceComponent> components,
        string hierarchyPath)
    {
        Components = components;
        Component = components.Last();
        HierarchyPath = hierarchyPath;
    }
}

internal static class ExpressionManagerItemCollector
{
    public static IReadOnlyList<ExpressionManagerExpressionItem> Collect(GameObject avatarRoot)
    {
        var expressions = avatarRoot
            .GetComponentsInChildren<ExpressionComponent>(true)
            .OrderBy(expression => GetSiblingOrderKey(avatarRoot.transform, expression.transform))
            .ToArray();

        return expressions
            .Select(expression => CreateItem(avatarRoot, expression))
            .ToArray();
    }

    public static IEnumerable<ExpressionManagerExpressionItem> Filter(
        IEnumerable<ExpressionManagerExpressionItem> items,
        string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return items;
        }

        var query = searchText.Trim();
        return items.Where(item =>
            item.Expression.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
            item.HierarchyPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public static IReadOnlyList<ExpressionManagerUnlinkedSourceItem> CollectUnlinkedSources(
        GameObject avatarRoot,
        IEnumerable<ExpressionManagerExpressionItem> expressionItems)
    {
        var linkedSources = expressionItems
            .SelectMany(item => item.EditableTargets)
            .OfType<ExpressionDataSourceComponent>()
            .ToHashSet();

        return avatarRoot
            .GetComponentsInChildren<ExpressionDataSourceComponent>(true)
            .GroupBy(source => source.gameObject)
            .SelectMany(group => CreateUnlinkedSourceStacks(group.Key, group, linkedSources))
            .OrderBy(stack => GetSiblingOrderKey(avatarRoot.transform, stack.Last().transform))
            .ThenBy(stack => Array.IndexOf(stack.Last().GetComponents<ExpressionDataSourceComponent>(), stack.Last()))
            .Select(stack => new ExpressionManagerUnlinkedSourceItem(
                stack,
                GetRelativePath(avatarRoot.transform, stack.Last().transform)))
            .ToArray();
    }

    public static IEnumerable<ExpressionManagerUnlinkedSourceItem> FilterUnlinkedSources(
        IEnumerable<ExpressionManagerUnlinkedSourceItem> items,
        string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return items;
        }

        var query = searchText.Trim();
        return items.Where(item =>
            item.HierarchyPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
            item.Components.Any(component =>
                component.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                component.GetType().Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
    }

    private static ExpressionManagerExpressionItem CreateItem(GameObject avatarRoot, ExpressionComponent expression)
    {
        var expressionDataComponents = expression
            .GetComponentsInChildren<ExpressionDataComponent>(true)
            .ToArray();

        var editableTargets = new List<Component>();
        editableTargets.AddRange(expressionDataComponents);

        var sourceComponents = expression
            .GetComponentsInChildren<ExpressionDataSourceComponent>(true)
            .Cast<Component>();
        AddDistinct(editableTargets, sourceComponents);

        var referencedSources = expressionDataComponents
            .SelectMany(component => component.DataReferences.SkipDestroyed())
            .SelectMany(ExpandSource)
            .Cast<Component>();
        AddDistinct(editableTargets, referencedSources);

        return new ExpressionManagerExpressionItem(
            avatarRoot,
            expression,
            GetRelativePath(avatarRoot.transform, expression.transform),
            expressionDataComponents,
            editableTargets,
            CountConditions(expression.transform, avatarRoot.transform));
    }

    private static void AddDistinct(List<Component> result, IEnumerable<Component> components)
    {
        foreach (var component in components)
        {
            if (component == null) continue;
            if (result.Contains(component)) continue;
            result.Add(component);
        }
    }

    private static IEnumerable<IReadOnlyList<ExpressionDataSourceComponent>> CreateUnlinkedSourceStacks(
        GameObject gameObject,
        IEnumerable<ExpressionDataSourceComponent> sources,
        ISet<ExpressionDataSourceComponent> linkedSources)
    {
        var orderedSources = sources
            .Where(source => source is BaseExpressionDataComponent or ExpressionOverrideComponent)
            .OrderBy(source => Array.IndexOf(gameObject.GetComponents<ExpressionDataSourceComponent>(), source))
            .ToArray();

        var consumedSources = new HashSet<ExpressionDataSourceComponent>();
        foreach (var source in orderedSources)
        {
            if (consumedSources.Contains(source)) continue;

            var stack = ExpandLocalDisplayStack(source).ToArray();
            foreach (var stackSource in stack)
            {
                consumedSources.Add(stackSource);
            }

            if (stack.Any(linkedSources.Contains)) continue;

            var displayStack = stack
                .Where(orderedSources.Contains)
                .ToArray();
            if (displayStack.Length > 0)
            {
                yield return displayStack;
            }
        }
    }

    private static IEnumerable<ExpressionDataSourceComponent> ExpandLocalDisplayStack(ExpressionDataSourceComponent source)
    {
        if (source is BaseExpressionDataComponent baseData)
        {
            return ExpandBaseStack(baseData);
        }

        if (source is ExpressionOverrideComponent expressionOverride)
        {
            var sameGameObjectSources = expressionOverride.GetComponents<ExpressionDataSourceComponent>();
            if (sameGameObjectSources.Any(candidate => candidate is BaseExpressionDataComponent))
            {
                return new[] { source };
            }

            return sameGameObjectSources.OfType<ExpressionOverrideComponent>();
        }

        return new[] { source };
    }

    private static IEnumerable<ExpressionDataSourceComponent> ExpandSource(ExpressionDataSourceComponent source)
    {
        return source switch
        {
            BaseExpressionDataComponent baseData => ExpandBaseStack(baseData),
            ExpressionOverrideComponent expressionOverride => ExpandOverrideSource(expressionOverride),
            _ => new[] { source }
        };
    }

    private static IEnumerable<ExpressionDataSourceComponent> ExpandBaseStack(BaseExpressionDataComponent baseData)
    {
        var sources = baseData.GetComponents<ExpressionDataSourceComponent>();
        var index = Array.IndexOf(sources, baseData);
        if (index < 0)
        {
            yield return baseData;
            yield break;
        }

        for (var i = index; i < sources.Length; i++)
        {
            var source = sources[i];
            if (i != index && source is BaseExpressionDataComponent) break;
            yield return source;
        }
    }

    private static IEnumerable<ExpressionDataSourceComponent> ExpandOverrideSource(ExpressionOverrideComponent expressionOverride)
    {
        foreach (var source in ExpressionDataComponent.GetOverrideBaseSources(expressionOverride, null))
        {
            yield return source;
        }

        yield return expressionOverride;
    }

    private static int CountConditions(Transform expressionTransform, Transform avatarRoot)
    {
        var count = 0;
        var current = expressionTransform;
        while (current != null)
        {
            foreach (var condition in current.GetComponents<ConditionComponent>())
            {
                count += condition.HandGestureConditions.Count;
                count += condition.ParameterConditions.Count;
            }

            if (current == avatarRoot) break;
            current = current.parent;
        }

        return count;
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (root == target) return target.name;

        var names = new Stack<string>();
        var current = target;
        while (current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return names.Count == 0 ? target.name : string.Join("/", names);
    }

    private static string GetSiblingOrderKey(Transform root, Transform target)
    {
        var indices = new Stack<int>();
        var current = target;
        while (current != null && current != root)
        {
            indices.Push(current.GetSiblingIndex());
            current = current.parent;
        }

        return string.Join("/", indices.Select(index => index.ToString("D8")));
    }
}
