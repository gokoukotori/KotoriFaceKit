namespace Aoyon.FaceTune
{
    internal static class ExpressionDataSourceUtility
    {
        internal static void AddAnimations(
            AnimationSet resultToAdd,
            GameObject owner,
            AnimationClip? clip,
            ClipImportOption clipOption,
            IReadOnlyList<BlendShapeWeightAnimation> blendShapeAnimations,
            bool allBlendShapeAnimationAsFacial,
            string bodyPath,
            bool ignoreZeroBlendShapeAnimations,
            IReadOnlyList<BlendShapeWeightAnimation>? facialStyleAnimations = null)
        {
            (List<BlendShapeWeightAnimation> facialAnimations, List<GenericAnimation> nonFacialAnimations) processedClip;
            if (clip == null)
            {
                processedClip = (new List<BlendShapeWeightAnimation>(), new List<GenericAnimation>());
            }
            else if (facialStyleAnimations == null)
            {
                processedClip = ProcessClip(owner, clip, clipOption, allBlendShapeAnimationAsFacial, bodyPath);
            }
            else
            {
                processedClip = ProcessClip(clip, clipOption, allBlendShapeAnimationAsFacial, bodyPath, facialStyleAnimations);
            }

            var (facialAnimations, nonFacialAnimations) = processedClip;
            AddBlendShapeAnimations(resultToAdd, facialAnimations, bodyPath, ignoreZeroBlendShapeAnimations);
            resultToAdd.AddRange(nonFacialAnimations);
            AddBlendShapeAnimations(resultToAdd, blendShapeAnimations, bodyPath, ignoreZeroBlendShapeAnimations);
        }

        internal static (List<BlendShapeWeightAnimation> facialAnimations, List<GenericAnimation> nonFacialAnimations) ProcessClip(
            GameObject owner,
            AnimationClip? clip,
            ClipImportOption clipOption,
            bool allBlendShapeAnimationAsFacial,
            string bodyPath)
        {
            var result = (facialAnimations: new List<BlendShapeWeightAnimation>(), nonFacialAnimations: new List<GenericAnimation>());
            if (clip == null) return result;

            var facialStyleAnimations = new List<BlendShapeWeightAnimation>();
            FacialStyleContext.TryGetFacialStyleAnimations(owner, facialStyleAnimations);
            return ProcessClip(clip, clipOption, allBlendShapeAnimationAsFacial, bodyPath, facialStyleAnimations);
        }

        private static (List<BlendShapeWeightAnimation> facialAnimations, List<GenericAnimation> nonFacialAnimations) ProcessClip(
            AnimationClip clip,
            ClipImportOption clipOption,
            bool allBlendShapeAnimationAsFacial,
            string bodyPath,
            IReadOnlyList<BlendShapeWeightAnimation> facialStyleAnimations)
        {
            var result = (facialAnimations: new List<BlendShapeWeightAnimation>(), nonFacialAnimations: new List<GenericAnimation>());
            var facialPath = allBlendShapeAnimationAsFacial ? null : bodyPath;
#if UNITY_EDITOR
            clip.ProcessAllBindings(clipOption, facialStyleAnimations, result.facialAnimations, result.nonFacialAnimations, facialPath);
#endif
            return result;
        }

        internal static void GetBlendShapes(
            ICollection<BlendShapeWeight> resultToAdd,
            GameObject owner,
            AnimationClip? clip,
            ClipImportOption clipOption,
            IReadOnlyList<BlendShapeWeightAnimation> blendShapeAnimations,
            bool allBlendShapeAnimationAsFacial,
            IReadOnlyList<BlendShapeWeightAnimation> facialAnimations,
            string bodyPath,
            bool ignoreZeroBlendShapeAnimations)
        {
            var animations = new List<BlendShapeWeightAnimation>();
            GetBlendShapeAnimations(animations, owner, clip, clipOption, blendShapeAnimations, allBlendShapeAnimationAsFacial, facialAnimations, bodyPath, ignoreZeroBlendShapeAnimations);
            foreach (var animation in animations)
            {
                resultToAdd.Add(animation.ToFirstFrameBlendShape());
            }
        }

        internal static void GetBlendShapeAnimations(
            ICollection<BlendShapeWeightAnimation> resultToAdd,
            GameObject owner,
            AnimationClip? clip,
            ClipImportOption clipOption,
            IReadOnlyList<BlendShapeWeightAnimation> blendShapeAnimations,
            bool allBlendShapeAnimationAsFacial,
            IReadOnlyList<BlendShapeWeightAnimation> facialAnimations,
            string bodyPath,
            bool ignoreZeroBlendShapeAnimations)
        {
            if (clip != null)
            {
                var facialPath = allBlendShapeAnimationAsFacial ? null : bodyPath;
#if UNITY_EDITOR
                var clipAnimations = new List<BlendShapeWeightAnimation>();
                clip.GetBlendShapeAnimations(clipAnimations, clipOption, facialAnimations, facialPath);
                AddBlendShapeAnimations(resultToAdd, clipAnimations, ignoreZeroBlendShapeAnimations);
#endif
            }

            AddBlendShapeAnimations(resultToAdd, blendShapeAnimations, ignoreZeroBlendShapeAnimations);
        }

        private static void AddBlendShapeAnimations(
            AnimationSet resultToAdd,
            IEnumerable<BlendShapeWeightAnimation> animations,
            string bodyPath,
            bool ignoreZeroBlendShapeAnimations)
        {
            var filtered = FilterBlendShapeAnimations(animations, ignoreZeroBlendShapeAnimations);
            resultToAdd.AddRange(filtered.ToGenericAnimations(bodyPath));
        }

        private static void AddBlendShapeAnimations(
            ICollection<BlendShapeWeightAnimation> resultToAdd,
            IEnumerable<BlendShapeWeightAnimation> animations,
            bool ignoreZeroBlendShapeAnimations)
        {
            foreach (var animation in FilterBlendShapeAnimations(animations, ignoreZeroBlendShapeAnimations))
            {
                resultToAdd.Add(animation);
            }
        }

        private static IEnumerable<BlendShapeWeightAnimation> FilterBlendShapeAnimations(
            IEnumerable<BlendShapeWeightAnimation> animations,
            bool ignoreZeroBlendShapeAnimations)
        {
            return ignoreZeroBlendShapeAnimations
                ? animations.Where(animation => !animation.IsZero)
                : animations;
        }
    }
}
