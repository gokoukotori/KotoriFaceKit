namespace Aoyon.FaceTune.Gui
{
    internal static class ExpressionDataAuthoringUtility
    {
        internal static BaseExpressionDataComponent AddBaseData(ExpressionDataComponent component)
        {
            return AddSource<BaseExpressionDataComponent>(component);
        }

        internal static ExpressionOverrideComponent AddExpressionOverride(ExpressionDataComponent component)
        {
            var expressionOverride = AddSource<ExpressionOverrideComponent>(component);
            var baseReferences = component.DataReferences
                .SkipDestroyed()
                .OfType<BaseExpressionDataComponent>()
                .ToArray();
            if (baseReferences.Length == 1)
            {
                Undo.RecordObject(expressionOverride, "Set Target Data");
                expressionOverride.TargetBase = baseReferences[0];
                EditorUtility.SetDirty(expressionOverride);
            }
            return expressionOverride;
        }

        internal static BaseExpressionDataComponent ConvertInlineToReference(ExpressionDataComponent component)
        {
            var baseData = AddBaseData(component);
            Undo.RecordObject(baseData, "Convert Inline To Reference");
            baseData.Clip = component.Clip;
            baseData.ClipOption = component.ClipOption;
            baseData.BlendShapeAnimations.AddRange(component.BlendShapeAnimations);
            baseData.AllBlendShapeAnimationAsFacial = component.AllBlendShapeAnimationAsFacial;
            EditorUtility.SetDirty(baseData);
            return baseData;
        }

        internal static BaseExpressionDataComponent CreateReferenceBaseFromClip(
            ExpressionDataComponent component,
            AnimationClip clip,
            ClipImportOption clipOption)
        {
            var baseData = AddBaseData(component);
            Undo.RecordObject(baseData, "Create Base Expression Data");
            baseData.Clip = clip;
            baseData.ClipOption = clipOption;
            EditorUtility.SetDirty(baseData);
            return baseData;
        }

        private static T AddSource<T>(ExpressionDataComponent component)
            where T : ExpressionDataSourceComponent
        {
            var source = Undo.AddComponent<T>(component.gameObject);
            Undo.RecordObject(component, $"Create {typeof(T).Name}");
            component.Mode = ExpressionDataMode.Reference;
            component.DataReferences.Add(source);
            EditorUtility.SetDirty(component);
            return source;
        }
    }
}
