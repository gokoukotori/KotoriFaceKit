namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPath)]
    public class ExpressionDataComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Expression Data";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public ExpressionDataMode Mode = ExpressionDataMode.Inline;

        public List<ExpressionDataSourceComponent> DataReferences = new();

        // AnimationClip
        public AnimationClip? Clip = null;
        public ClipImportOption ClipOption = ClipImportOption.NonZero;

        // Manual
        public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        public bool AllBlendShapeAnimationAsFacial = false;

        internal void GetAnimations(AnimationSet resultToAdd, AvatarContext avatarContext)
        {
            switch (Mode)
            {
                case ExpressionDataMode.Inline:
                    GetInlineAnimations(resultToAdd, avatarContext);
                    break;
                case ExpressionDataMode.Reference:
                    GetReferenceAnimations(resultToAdd, avatarContext);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Mode), Mode, null);
            }
        }

        private void GetInlineAnimations(AnimationSet resultToAdd, AvatarContext avatarContext)
        {
            ExpressionDataSourceUtility.AddAnimations(
                resultToAdd,
                gameObject,
                Clip,
                ClipOption,
                BlendShapeAnimations,
                AllBlendShapeAnimationAsFacial,
                avatarContext.BodyPath,
                false);
        }

        private void GetReferenceAnimations(AnimationSet resultToAdd, AvatarContext avatarContext)
        {
            var facialStyleAnimations = new List<BlendShapeWeightAnimation>();
            FacialStyleContext.TryGetFacialStyleAnimations(gameObject, facialStyleAnimations);

            foreach (var source in GetReferenceSources(null))
            {
                source.GetAnimations(resultToAdd, avatarContext, facialStyleAnimations);
            }
        }

        internal (List<BlendShapeWeightAnimation> facialAnimations, List<GenericAnimation> nonFacialAnimations) ProcessClip(string bodyPath)
        {
            return ExpressionDataSourceUtility.ProcessClip(gameObject, Clip, ClipOption, AllBlendShapeAnimationAsFacial, bodyPath);
        }

        internal void GetBlendShapes(ICollection<BlendShapeWeight> resultToAdd, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, string bodyPath, IObserveContext? observeContext = null)
        {
            observeContext ??= new NonObserveContext();
            observeContext.Observe(this);

            switch (Mode)
            {
                case ExpressionDataMode.Inline:
                    ExpressionDataSourceUtility.GetBlendShapes(
                        resultToAdd,
                        gameObject,
                        Clip,
                        ClipOption,
                        BlendShapeAnimations,
                        AllBlendShapeAnimationAsFacial,
                        facialAnimations,
                        bodyPath,
                        false);
                    break;
                case ExpressionDataMode.Reference:
                    GetReferenceBlendShapes(resultToAdd, facialAnimations, bodyPath, observeContext);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Mode), Mode, null);
            }
        }

        internal void GetBlendShapeAnimations(ICollection<BlendShapeWeightAnimation> resultToAdd, IReadOnlyList<BlendShapeWeightAnimation> facialAnimations, string bodyPath, IObserveContext? observeContext = null)
        {
            observeContext ??= new NonObserveContext();
            observeContext.Observe(this);

            switch (Mode)
            {
                case ExpressionDataMode.Inline:
                    ExpressionDataSourceUtility.GetBlendShapeAnimations(
                        resultToAdd,
                        gameObject,
                        Clip,
                        ClipOption,
                        BlendShapeAnimations,
                        AllBlendShapeAnimationAsFacial,
                        facialAnimations,
                        bodyPath,
                        false);
                    break;
                case ExpressionDataMode.Reference:
                    GetReferenceBlendShapeAnimations(resultToAdd, facialAnimations, bodyPath, observeContext);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Mode), Mode, null);
            }
        }

        private void GetReferenceBlendShapes(
            ICollection<BlendShapeWeight> resultToAdd,
            IReadOnlyList<BlendShapeWeightAnimation> facialAnimations,
            string bodyPath,
            IObserveContext observeContext)
        {
            foreach (var source in GetReferenceSources(observeContext))
            {
                source.GetBlendShapes(resultToAdd, facialAnimations, bodyPath, observeContext);
            }
        }

        private void GetReferenceBlendShapeAnimations(
            ICollection<BlendShapeWeightAnimation> resultToAdd,
            IReadOnlyList<BlendShapeWeightAnimation> facialAnimations,
            string bodyPath,
            IObserveContext observeContext)
        {
            foreach (var source in GetReferenceSources(observeContext))
            {
                source.GetBlendShapeAnimations(resultToAdd, facialAnimations, bodyPath, observeContext);
            }
        }

        private IEnumerable<ExpressionDataSourceComponent> GetReferenceSources(IObserveContext? observeContext)
        {
            var references = DataReferences
                .SkipDestroyed()
                .ToArray();
            var consumedSources = new HashSet<ExpressionDataSourceComponent>();

            foreach (var reference in references)
            {
                if (consumedSources.Contains(reference)) continue;

                foreach (var source in ExpandReferenceSource(reference, observeContext, consumedSources))
                {
                    yield return source;
                }
            }

            foreach (var sameGameObjectSource in GetExpandedSourceOnlySources(gameObject, observeContext, consumedSources))
            {
                yield return sameGameObjectSource;
            }
        }

        internal static IEnumerable<ExpressionDataSourceComponent> GetExpandedSourceOnlySources(
            GameObject gameObject,
            IObserveContext? observeContext)
        {
            return GetExpandedSourceOnlySources(gameObject, observeContext, new HashSet<ExpressionDataSourceComponent>());
        }

        private static IEnumerable<ExpressionDataSourceComponent> GetExpandedSourceOnlySources(
            GameObject gameObject,
            IObserveContext? observeContext,
            ISet<ExpressionDataSourceComponent> consumedSources)
        {
            foreach (var source in GetSameGameObjectSources(gameObject, observeContext))
            {
                if (consumedSources.Contains(source)) continue;

                foreach (var expandedSource in ExpandReferenceSource(source, observeContext, consumedSources))
                {
                    yield return expandedSource;
                }
            }
        }

        internal static IEnumerable<ExpressionDataSourceComponent> GetOverrideBaseSources(
            ExpressionOverrideComponent expressionOverride,
            IObserveContext? observeContext)
        {
            var result = new List<ExpressionDataSourceComponent>();
            var chain = new HashSet<ExpressionOverrideComponent>();
            if (!TryCollectOverrideBaseSources(expressionOverride, observeContext, result, chain))
            {
                yield break;
            }

            foreach (var source in result)
            {
                yield return source;
            }
        }

        private static IEnumerable<ExpressionDataSourceComponent> ExpandReferenceSource(
            ExpressionDataSourceComponent source,
            IObserveContext? observeContext,
            ISet<ExpressionDataSourceComponent> consumedSources)
        {
            var result = new List<ExpressionDataSourceComponent>();
            var chain = new HashSet<ExpressionOverrideComponent>();
            if (!TryCollectReferenceSource(source, observeContext, consumedSources, result, chain))
            {
                yield break;
            }

            foreach (var expandedSource in result)
            {
                yield return expandedSource;
            }
        }

        private static bool TryCollectReferenceSource(
            ExpressionDataSourceComponent source,
            IObserveContext? observeContext,
            ISet<ExpressionDataSourceComponent>? consumedSources,
            List<ExpressionDataSourceComponent> resultToAdd,
            ISet<ExpressionOverrideComponent> chain)
        {
            return source switch
            {
                BaseExpressionDataComponent baseData => TryCollectBaseStackForward(baseData, observeContext, consumedSources, resultToAdd),
                ExpressionOverrideComponent expressionOverride => TryCollectOverrideSource(expressionOverride, observeContext, consumedSources, resultToAdd, chain),
                _ => true
            };
        }

        private static bool TryCollectOverrideSource(
            ExpressionOverrideComponent expressionOverride,
            IObserveContext? observeContext,
            ISet<ExpressionDataSourceComponent>? consumedSources,
            List<ExpressionDataSourceComponent> resultToAdd,
            ISet<ExpressionOverrideComponent> chain)
        {
            if (TryGetOverrideOnlyStack(expressionOverride, observeContext, out var overrideOnlyStack, out var overrideOnlyStackIndex))
            {
                return TryCollectOverrideOnlyStackThroughOverride(
                    overrideOnlyStack,
                    overrideOnlyStackIndex,
                    observeContext,
                    consumedSources,
                    resultToAdd,
                    chain);
            }

            var target = expressionOverride.TargetBase;
            if (target == null)
            {
                return HasSameGameObjectBaseSource(expressionOverride, observeContext) &&
                       TryCollectLocalStackThroughOverride(expressionOverride, observeContext, consumedSources, resultToAdd);
            }

            if (target.gameObject == expressionOverride.gameObject && HasSameGameObjectBaseSource(expressionOverride, observeContext))
            {
                return TryCollectLocalStackThroughOverride(expressionOverride, observeContext, consumedSources, resultToAdd);
            }

            if (!chain.Add(expressionOverride)) return false;

            var resultCount = resultToAdd.Count;
            try
            {
                if (!TryCollectReferenceSource(target, observeContext, null, resultToAdd, chain))
                {
                    resultToAdd.RemoveRange(resultCount, resultToAdd.Count - resultCount);
                    return false;
                }
            }
            finally
            {
                chain.Remove(expressionOverride);
            }

            AddSource(expressionOverride, observeContext, consumedSources, resultToAdd);
            return true;
        }

        private static bool TryCollectOverrideBaseSources(
            ExpressionOverrideComponent expressionOverride,
            IObserveContext? observeContext,
            List<ExpressionDataSourceComponent> resultToAdd,
            ISet<ExpressionOverrideComponent> chain)
        {
            if (HasPriorSameGameObjectBaseSource(expressionOverride, observeContext))
            {
                return TryCollectLocalStackBeforeOverride(expressionOverride, observeContext, resultToAdd);
            }

            if (TryGetOverrideOnlyStack(expressionOverride, observeContext, out var overrideOnlyStack, out var overrideOnlyStackIndex))
            {
                return TryCollectOverrideOnlyStackBeforeOverride(
                    overrideOnlyStack,
                    overrideOnlyStackIndex,
                    observeContext,
                    resultToAdd,
                    chain);
            }

            var target = expressionOverride.TargetBase;
            if (target == null)
            {
                return true;
            }

            if (target.gameObject == expressionOverride.gameObject && HasSameGameObjectBaseSource(expressionOverride, observeContext))
            {
                return TryCollectLocalStackBeforeOverride(expressionOverride, observeContext, resultToAdd);
            }

            if (!chain.Add(expressionOverride)) return false;

            try
            {
                return TryCollectReferenceSource(target, observeContext, null, resultToAdd, chain);
            }
            finally
            {
                chain.Remove(expressionOverride);
            }
        }

        private static bool TryCollectBaseStackForward(
            BaseExpressionDataComponent baseData,
            IObserveContext? observeContext,
            ISet<ExpressionDataSourceComponent>? consumedSources,
            List<ExpressionDataSourceComponent> resultToAdd)
        {
            var sources = GetSameGameObjectSources(baseData.gameObject, observeContext).ToArray();
            var index = Array.IndexOf(sources, baseData);
            if (index < 0) return true;

            for (var i = index; i < sources.Length; i++)
            {
                var source = sources[i];
                if (i != index && source is BaseExpressionDataComponent) break;
                AddSource(source, observeContext, consumedSources, resultToAdd);
            }

            return true;
        }

        private static bool TryCollectOverrideOnlyStackThroughOverride(
            IReadOnlyList<ExpressionOverrideComponent> overrideOnlyStack,
            int overrideOnlyStackIndex,
            IObserveContext? observeContext,
            ISet<ExpressionDataSourceComponent>? consumedSources,
            List<ExpressionDataSourceComponent> resultToAdd,
            ISet<ExpressionOverrideComponent> chain)
        {
            var anchor = overrideOnlyStack[0];
            var anchorConsumed = consumedSources?.Contains(anchor) == true;
            if (!anchorConsumed && !TryCollectOverrideOnlyStackTarget(anchor, observeContext, resultToAdd, chain))
            {
                return false;
            }

            for (var i = 0; i <= overrideOnlyStackIndex; i++)
            {
                var expressionOverride = overrideOnlyStack[i];
                if (consumedSources?.Contains(expressionOverride) == true) continue;

                AddSource(expressionOverride, observeContext, consumedSources, resultToAdd);
            }

            return true;
        }

        private static bool TryCollectOverrideOnlyStackBeforeOverride(
            IReadOnlyList<ExpressionOverrideComponent> overrideOnlyStack,
            int overrideOnlyStackIndex,
            IObserveContext? observeContext,
            List<ExpressionDataSourceComponent> resultToAdd,
            ISet<ExpressionOverrideComponent> chain)
        {
            var anchor = overrideOnlyStack[0];
            if (!TryCollectOverrideOnlyStackTarget(anchor, observeContext, resultToAdd, chain))
            {
                return false;
            }

            for (var i = 0; i < overrideOnlyStackIndex; i++)
            {
                AddSource(overrideOnlyStack[i], observeContext, null, resultToAdd);
            }

            return true;
        }

        private static bool TryCollectOverrideOnlyStackTarget(
            ExpressionOverrideComponent anchor,
            IObserveContext? observeContext,
            List<ExpressionDataSourceComponent> resultToAdd,
            ISet<ExpressionOverrideComponent> chain)
        {
            var target = anchor.TargetBase;
            if (target == null || target.gameObject == anchor.gameObject) return false;
            if (!chain.Add(anchor)) return false;

            var resultCount = resultToAdd.Count;
            try
            {
                if (!TryCollectReferenceSource(target, observeContext, null, resultToAdd, chain))
                {
                    resultToAdd.RemoveRange(resultCount, resultToAdd.Count - resultCount);
                    return false;
                }
            }
            finally
            {
                chain.Remove(anchor);
            }

            return true;
        }

        private static bool TryCollectLocalStackThroughOverride(
            ExpressionOverrideComponent expressionOverride,
            IObserveContext? observeContext,
            ISet<ExpressionDataSourceComponent>? consumedSources,
            List<ExpressionDataSourceComponent> resultToAdd)
        {
            var sources = GetSameGameObjectSources(expressionOverride.gameObject, observeContext).ToArray();
            var index = Array.IndexOf(sources, expressionOverride);
            if (index < 0) return true;

            var startIndex = GetStackStartIndex(sources, index);
            for (var i = startIndex; i <= index; i++)
            {
                AddSource(sources[i], observeContext, consumedSources, resultToAdd);
            }

            return true;
        }

        private static bool TryCollectLocalStackBeforeOverride(
            ExpressionOverrideComponent expressionOverride,
            IObserveContext? observeContext,
            List<ExpressionDataSourceComponent> resultToAdd)
        {
            var sources = GetSameGameObjectSources(expressionOverride.gameObject, observeContext).ToArray();
            var index = Array.IndexOf(sources, expressionOverride);
            if (index <= 0) return true;

            var startIndex = GetStackStartIndex(sources, index);
            for (var i = startIndex; i < index; i++)
            {
                AddSource(sources[i], observeContext, null, resultToAdd);
            }

            return true;
        }

        private static bool HasPriorSameGameObjectBaseSource(
            ExpressionOverrideComponent expressionOverride,
            IObserveContext? observeContext)
        {
            var sources = GetSameGameObjectSources(expressionOverride.gameObject, observeContext).ToArray();
            var index = Array.IndexOf(sources, expressionOverride);
            return index > 0 && sources.Take(index).Any(source => source is BaseExpressionDataComponent);
        }

        private static bool HasSameGameObjectBaseSource(
            ExpressionOverrideComponent expressionOverride,
            IObserveContext? observeContext)
        {
            return GetSameGameObjectSources(expressionOverride.gameObject, observeContext)
                .Any(source => source is BaseExpressionDataComponent);
        }

        private static bool TryGetOverrideOnlyStack(
            ExpressionOverrideComponent expressionOverride,
            IObserveContext? observeContext,
            out ExpressionOverrideComponent[] overrideOnlyStack,
            out int index)
        {
            var sources = GetSameGameObjectSources(expressionOverride.gameObject, observeContext).ToArray();
            if (sources.Any(source => source is BaseExpressionDataComponent))
            {
                overrideOnlyStack = Array.Empty<ExpressionOverrideComponent>();
                index = -1;
                return false;
            }

            overrideOnlyStack = sources
                .OfType<ExpressionOverrideComponent>()
                .ToArray();
            index = Array.IndexOf(overrideOnlyStack, expressionOverride);
            return overrideOnlyStack.Length > 1 && index >= 0;
        }

        private static int GetStackStartIndex(IReadOnlyList<ExpressionDataSourceComponent> sources, int index)
        {
            for (var i = index; i >= 0; i--)
            {
                if (sources[i] is BaseExpressionDataComponent)
                {
                    return i;
                }
            }

            return index;
        }

        private static void AddSource(
            ExpressionDataSourceComponent source,
            IObserveContext? observeContext,
            ISet<ExpressionDataSourceComponent>? consumedSources,
            ICollection<ExpressionDataSourceComponent> resultToAdd)
        {
            observeContext?.Observe(source);
            consumedSources?.Add(source);
            resultToAdd.Add(source);
        }

        private static IEnumerable<ExpressionDataSourceComponent> GetSameGameObjectSources(
            GameObject gameObject,
            IObserveContext? observeContext)
        {
            var sameGameObjectSources = new List<ExpressionDataSourceComponent>();
            if (observeContext == null)
            {
                gameObject.GetComponents(sameGameObjectSources);
            }
            else
            {
                observeContext.GetComponents(gameObject, sameGameObjectSources);
            }

            foreach (var source in sameGameObjectSources.SkipDestroyed())
            {
                yield return source;
            }
        }
    }
}
