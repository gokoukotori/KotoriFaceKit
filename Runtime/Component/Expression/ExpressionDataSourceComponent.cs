namespace Aoyon.FaceTune
{
    public abstract class ExpressionDataSourceComponent : FaceTuneTagComponent
    {
        [TextArea(2, 6)]
        public string Memo = string.Empty;

        public AnimationClip? Clip = null;
        public ClipImportOption ClipOption = ClipImportOption.NonZero;
        public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();
        public bool AllBlendShapeAnimationAsFacial = false;

        internal virtual bool IgnoreZeroBlendShapeAnimations => false;

        internal void GetAnimations(AnimationSet resultToAdd, AvatarContext avatarContext)
        {
            GetAnimations(resultToAdd, avatarContext, null);
        }

        internal void GetAnimations(
            AnimationSet resultToAdd,
            AvatarContext avatarContext,
            IReadOnlyList<BlendShapeWeightAnimation>? facialStyleAnimations)
        {
            ExpressionDataSourceUtility.AddAnimations(
                resultToAdd,
                gameObject,
                Clip,
                ClipOption,
                BlendShapeAnimations,
                AllBlendShapeAnimationAsFacial,
                avatarContext.BodyPath,
                IgnoreZeroBlendShapeAnimations,
                facialStyleAnimations);
        }

        internal (List<BlendShapeWeightAnimation> facialAnimations, List<GenericAnimation> nonFacialAnimations) ProcessClip(string bodyPath)
        {
            return ExpressionDataSourceUtility.ProcessClip(gameObject, Clip, ClipOption, AllBlendShapeAnimationAsFacial, bodyPath);
        }

        internal void GetBlendShapes(
            ICollection<BlendShapeWeight> resultToAdd,
            IReadOnlyList<BlendShapeWeightAnimation> facialAnimations,
            string bodyPath,
            IObserveContext? observeContext = null)
        {
            observeContext ??= new NonObserveContext();
            observeContext.Observe(this);

            ExpressionDataSourceUtility.GetBlendShapes(
                resultToAdd,
                gameObject,
                Clip,
                ClipOption,
                BlendShapeAnimations,
                AllBlendShapeAnimationAsFacial,
                facialAnimations,
                bodyPath,
                IgnoreZeroBlendShapeAnimations);
        }

        internal void GetBlendShapeAnimations(
            ICollection<BlendShapeWeightAnimation> resultToAdd,
            IReadOnlyList<BlendShapeWeightAnimation> facialAnimations,
            string bodyPath,
            IObserveContext? observeContext = null)
        {
            observeContext ??= new NonObserveContext();
            observeContext.Observe(this);

            ExpressionDataSourceUtility.GetBlendShapeAnimations(
                resultToAdd,
                gameObject,
                Clip,
                ClipOption,
                BlendShapeAnimations,
                AllBlendShapeAnimationAsFacial,
                facialAnimations,
                bodyPath,
                IgnoreZeroBlendShapeAnimations);
        }
    }
}
