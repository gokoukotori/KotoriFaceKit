namespace Aoyon.FaceTune.Gui;

internal sealed class ExpressionManagerExpressionItem
{
    public ExpressionComponent Expression { get; }
    public string HierarchyPath { get; }
    public IReadOnlyList<ExpressionDataComponent> ExpressionDataComponents { get; }
    public IReadOnlyList<Component> EditableTargets { get; }
    public int ConditionCount { get; }

    public ExpressionManagerExpressionItem(
        ExpressionComponent expression,
        string hierarchyPath,
        IReadOnlyList<ExpressionDataComponent> expressionDataComponents,
        IReadOnlyList<Component> editableTargets,
        int conditionCount)
    {
        Expression = expression;
        HierarchyPath = hierarchyPath;
        ExpressionDataComponents = expressionDataComponents;
        EditableTargets = editableTargets;
        ConditionCount = conditionCount;
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
            .Cast<Component>();
        AddDistinct(editableTargets, referencedSources);

        return new ExpressionManagerExpressionItem(
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
