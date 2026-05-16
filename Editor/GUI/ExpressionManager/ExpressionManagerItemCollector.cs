namespace Aoyon.FaceTune.Gui;

internal interface IExpressionManagerDisplayItem
{
    public string HierarchyPath { get; }
    public int ExpressionCount { get; }
    public IReadOnlyList<ExpressionManagerExpressionItem> Expressions { get; }
}

internal sealed class ExpressionManagerExpressionItem
    : IExpressionManagerDisplayItem
{
    public GameObject AvatarRoot { get; }
    public ExpressionComponent Expression { get; }
    public string HierarchyPath { get; }
    public IReadOnlyList<ExpressionDataComponent> ExpressionDataComponents { get; }
    public IReadOnlyList<Component> EditableTargets { get; }
    public int ConditionCount { get; }
    public int ExpressionCount => 1;
    public IReadOnlyList<ExpressionManagerExpressionItem> Expressions => new[] { this };

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

internal sealed class ExpressionManagerPatternGroup : IExpressionManagerDisplayItem
{
    public PatternComponent Pattern { get; }
    public string HierarchyPath { get; }
    public IReadOnlyList<ExpressionManagerExpressionItem> Expressions { get; }
    public int ExpressionCount => Expressions.Count;

    public ExpressionManagerPatternGroup(
        PatternComponent pattern,
        string hierarchyPath,
        IReadOnlyList<ExpressionManagerExpressionItem> expressions)
    {
        Pattern = pattern;
        HierarchyPath = hierarchyPath;
        Expressions = expressions;
    }
}

internal sealed class ExpressionManagerPresetGroup : IExpressionManagerDisplayItem
{
    public PresetComponent Preset { get; }
    public string HierarchyPath { get; }
    public IReadOnlyList<ExpressionManagerPatternGroup> Patterns { get; }
    public IReadOnlyList<ExpressionManagerExpressionItem> Expressions { get; }
    public int ExpressionCount => Expressions.Count;

    public ExpressionManagerPresetGroup(
        PresetComponent preset,
        string hierarchyPath,
        IReadOnlyList<ExpressionManagerPatternGroup> patterns)
    {
        Preset = preset;
        HierarchyPath = hierarchyPath;
        Patterns = patterns;
        Expressions = patterns
            .SelectMany(pattern => pattern.Expressions)
            .ToArray();
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

    public static IReadOnlyList<IExpressionManagerDisplayItem> CollectDisplayItems(GameObject avatarRoot)
    {
        var expressionItems = Collect(avatarRoot);
        var assignedExpressions = new HashSet<ExpressionComponent>();
        var displayItems = new List<IExpressionManagerDisplayItem>();

        foreach (var preset in avatarRoot.GetComponentsInChildren<PresetComponent>(true))
        {
            var group = CreatePresetGroup(avatarRoot, preset, expressionItems, assignedExpressions);
            if (group != null)
            {
                displayItems.Add(group);
            }
        }

        foreach (var pattern in avatarRoot.GetComponentsInChildren<PatternComponent>(true))
        {
            if (GetNearestComponentInParents<PresetComponent>(pattern.transform, avatarRoot.transform) != null) continue;

            var group = CreatePatternGroup(
                avatarRoot,
                pattern,
                expressionItems,
                assignedExpressions,
                requiredPreset: null);
            if (group != null)
            {
                displayItems.Add(group);
            }
        }

        foreach (var item in expressionItems)
        {
            if (assignedExpressions.Contains(item.Expression)) continue;
            displayItems.Add(item);
        }

        return displayItems
            .OrderBy(item => GetDisplayOrderKey(avatarRoot.transform, item))
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
        return items.Where(item => MatchesExpression(item, query));
    }

    public static IEnumerable<IExpressionManagerDisplayItem> FilterDisplayItems(
        IEnumerable<IExpressionManagerDisplayItem> items,
        string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return items;
        }

        var query = searchText.Trim();
        return items
            .Select(item => FilterDisplayItem(item, query))
            .Where(item => item != null)
            .Select(item => item!);
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

    private static ExpressionManagerPresetGroup? CreatePresetGroup(
        GameObject avatarRoot,
        PresetComponent preset,
        IReadOnlyList<ExpressionManagerExpressionItem> expressionItems,
        ISet<ExpressionComponent> assignedExpressions)
    {
        var patterns = preset
            .GetComponentsInChildren<PatternComponent>(true)
            .OrderBy(pattern => GetSiblingOrderKey(avatarRoot.transform, pattern.transform))
            .Select(pattern => CreatePatternGroup(
                avatarRoot,
                pattern,
                expressionItems,
                assignedExpressions,
                preset))
            .Where(group => group != null)
            .Select(group => group!)
            .ToArray();

        return patterns.Length == 0
            ? null
            : new ExpressionManagerPresetGroup(
                preset,
                GetRelativePath(avatarRoot.transform, preset.transform),
                patterns);
    }

    private static ExpressionManagerPatternGroup? CreatePatternGroup(
        GameObject avatarRoot,
        PatternComponent pattern,
        IReadOnlyList<ExpressionManagerExpressionItem> expressionItems,
        ISet<ExpressionComponent> assignedExpressions,
        PresetComponent? requiredPreset)
    {
        var expressions = expressionItems
            .Where(item => !assignedExpressions.Contains(item.Expression))
            .Where(item => GetNearestComponentInParents<PatternComponent>(item.Expression.transform, avatarRoot.transform) == pattern)
            .Where(item => GetNearestComponentInParents<PresetComponent>(item.Expression.transform, avatarRoot.transform) == requiredPreset)
            .ToArray();

        if (expressions.Length == 0) return null;

        foreach (var expression in expressions)
        {
            assignedExpressions.Add(expression.Expression);
        }

        return new ExpressionManagerPatternGroup(
            pattern,
            GetRelativePath(avatarRoot.transform, pattern.transform),
            expressions);
    }

    private static IExpressionManagerDisplayItem? FilterDisplayItem(
        IExpressionManagerDisplayItem item,
        string query)
    {
        return item switch
        {
            ExpressionManagerPresetGroup presetGroup => FilterPresetGroup(presetGroup, query),
            ExpressionManagerPatternGroup patternGroup => FilterPatternGroup(patternGroup, query),
            ExpressionManagerExpressionItem expressionItem => MatchesExpression(expressionItem, query) ? expressionItem : null,
            _ => null
        };
    }

    private static ExpressionManagerPresetGroup? FilterPresetGroup(
        ExpressionManagerPresetGroup group,
        string query)
    {
        if (MatchesComponent(group.Preset, group.HierarchyPath, query))
        {
            return group;
        }

        var patterns = group.Patterns
            .Select(pattern => FilterPatternGroup(pattern, query))
            .Where(pattern => pattern != null)
            .Select(pattern => pattern!)
            .ToArray();

        return patterns.Length == 0
            ? null
            : new ExpressionManagerPresetGroup(group.Preset, group.HierarchyPath, patterns);
    }

    private static ExpressionManagerPatternGroup? FilterPatternGroup(
        ExpressionManagerPatternGroup group,
        string query)
    {
        if (MatchesComponent(group.Pattern, group.HierarchyPath, query))
        {
            return group;
        }

        var expressions = group.Expressions
            .Where(item => MatchesExpression(item, query))
            .ToArray();

        return expressions.Length == 0
            ? null
            : new ExpressionManagerPatternGroup(group.Pattern, group.HierarchyPath, expressions);
    }

    private static bool MatchesComponent(Component component, string hierarchyPath, string query)
    {
        return component.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               hierarchyPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool MatchesExpression(ExpressionManagerExpressionItem item, string query)
    {
        return item.Expression.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               item.HierarchyPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
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

    private static T? GetNearestComponentInParents<T>(Transform target, Transform root) where T : Component
    {
        var current = target;
        while (current != null)
        {
            if (current.TryGetComponent<T>(out var component))
            {
                return component;
            }

            if (current == root) break;
            current = current.parent;
        }

        return null;
    }

    private static string GetDisplayOrderKey(Transform root, IExpressionManagerDisplayItem item)
    {
        return item switch
        {
            ExpressionManagerPresetGroup presetGroup => GetSiblingOrderKey(root, presetGroup.Preset.transform),
            ExpressionManagerPatternGroup patternGroup => GetSiblingOrderKey(root, patternGroup.Pattern.transform),
            ExpressionManagerExpressionItem expressionItem => GetSiblingOrderKey(root, expressionItem.Expression.transform),
            _ => string.Empty
        };
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
