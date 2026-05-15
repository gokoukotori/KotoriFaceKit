using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

internal static class ExpressionShapesEditorLauncher
{
    public static void Open(ExpressionDataComponent component)
    {
        if (!CustomEditorUtility.TryGetContext(component.gameObject, out var context))
        {
            throw new InvalidOperationException("Context not found");
        }

        var bodyPath = context.BodyPath;
        var facialStyleAnimations = new List<BlendShapeWeightAnimation>();
        FacialStyleContext.TryGetFacialStyleAnimations(component.gameObject, facialStyleAnimations);

        var baseSet = new BlendShapeWeightSet();
        baseSet.AddRange(facialStyleAnimations.ToFirstFrameBlendShapes());
        if (component.TryGetComponentInParent<ExpressionComponent>(true, out var expressionComponent))
        {
            foreach (var upperData in expressionComponent.GetComponentsInChildren<ExpressionDataComponent>())
            {
                if (upperData == component) break;
                upperData.GetBlendShapes(baseSet, facialStyleAnimations, bodyPath);
            }
        }

        baseSet.AddRange(component.ProcessClip(bodyPath).facialAnimations.ToFirstFrameBlendShapes());

        var defaultOverride = new BlendShapeWeightSet();
        defaultOverride.AddRange(component.BlendShapeAnimations.ToFirstFrameBlendShapes());

        CustomEditorUtility.OpenEditor(
            component.gameObject,
            new ExpressionDataTargeting { Target = component },
            defaultOverride,
            baseSet);
    }

    public static void Open(ExpressionDataSourceComponent component)
    {
        if (!CustomEditorUtility.TryGetContext(component.gameObject, out var context))
        {
            throw new InvalidOperationException("Context not found");
        }

        var bodyPath = context.BodyPath;
        var facialStyleAnimations = new List<BlendShapeWeightAnimation>();
        FacialStyleContext.TryGetFacialStyleAnimations(component.gameObject, facialStyleAnimations);

        var baseSet = new BlendShapeWeightSet();
        baseSet.AddRange(facialStyleAnimations.ToFirstFrameBlendShapes());
        if (component is ExpressionOverrideComponent expressionOverride)
        {
            foreach (var source in ExpressionDataComponent.GetOverrideBaseSources(expressionOverride, null))
            {
                source.GetBlendShapes(baseSet, facialStyleAnimations, bodyPath);
            }
        }

        baseSet.AddRange(component.ProcessClip(bodyPath).facialAnimations.ToFirstFrameBlendShapes());

        var defaultOverride = new BlendShapeWeightSet();
        defaultOverride.AddRange(component.BlendShapeAnimations.ToFirstFrameBlendShapes());

        CustomEditorUtility.OpenEditor(component.gameObject, CreateTargeting(component), defaultOverride, baseSet);
    }

    private static IShapesEditorTargeting CreateTargeting(ExpressionDataSourceComponent component)
    {
        return component switch
        {
            BaseExpressionDataComponent baseData => new ExpressionDataSourceTargeting<BaseExpressionDataComponent> { Target = baseData },
            ExpressionOverrideComponent expressionOverride => new ExpressionDataSourceTargeting<ExpressionOverrideComponent> { Target = expressionOverride },
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, null)
        };
    }
}
