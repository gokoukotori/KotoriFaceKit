namespace Aoyon.FaceTune.Gui;

internal static class ExpressionManagerPreviewBlendShapeCollector
{
    public static BlendShapeWeightSet Collect(ExpressionComponent expression, AvatarContext avatarContext)
    {
        var result = new BlendShapeWeightSet();

        if (!expression.FacialSettings.EnableBlending)
        {
            result.AddRange(avatarContext.SafeZeroBlendShapes);
        }

        using var _facialStyleAnimations = ListPool<BlendShapeWeightAnimation>.Get(out var facialStyleAnimations);
        if (FacialStyleContext.TryGetFacialStyleAnimations(expression.gameObject, facialStyleAnimations))
        {
            result.AddRange(facialStyleAnimations.ToFirstFrameBlendShapes());
        }

        var dataComponents = expression.GetComponentsInChildren<ExpressionDataComponent>(true);
        foreach (var dataComponent in dataComponents)
        {
            dataComponent.GetBlendShapes(result, facialStyleAnimations, avatarContext.BodyPath);
        }

        return result;
    }

    public static BlendShapeWeightSet Collect(ExpressionDataSourceComponent source, AvatarContext avatarContext)
    {
        var result = new BlendShapeWeightSet();

        using var _facialStyleAnimations = ListPool<BlendShapeWeightAnimation>.Get(out var facialStyleAnimations);
        if (FacialStyleContext.TryGetFacialStyleAnimations(source.gameObject, facialStyleAnimations))
        {
            result.AddRange(facialStyleAnimations.ToFirstFrameBlendShapes());
        }

        if (source is ExpressionOverrideComponent expressionOverride)
        {
            foreach (var baseSource in ExpressionDataComponent.GetOverrideBaseSources(expressionOverride, null))
            {
                baseSource.GetBlendShapes(result, facialStyleAnimations, avatarContext.BodyPath);
            }
        }

        source.GetBlendShapes(result, facialStyleAnimations, avatarContext.BodyPath);
        return result;
    }
}
