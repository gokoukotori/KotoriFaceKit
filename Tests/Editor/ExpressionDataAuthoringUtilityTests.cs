using Aoyon.FaceTune.Gui;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Aoyon.FaceTune.Tests
{
    public class ExpressionDataAuthoringUtilityTests
    {
        private GameObject _gameObject = null!;
        private ExpressionDataComponent _expressionData = null!;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Expression");
            _expressionData = _gameObject.AddComponent<ExpressionDataComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void AddBaseDataCreatesComponentOnSameGameObjectAndReferencesIt()
        {
            var baseData = ExpressionDataAuthoringUtility.AddBaseData(_expressionData);

            Assert.That(baseData.gameObject, Is.SameAs(_gameObject));
            Assert.That(_expressionData.DataReferences, Is.EqualTo(new ExpressionDataSourceComponent[] { baseData }));
            Assert.That(_expressionData.Mode, Is.EqualTo(ExpressionDataMode.Reference));
        }

        [Test]
        public void AddExpressionOverrideSetsOnlyBaseAsTargetBase()
        {
            var baseData = ExpressionDataAuthoringUtility.AddBaseData(_expressionData);

            var expressionOverride = ExpressionDataAuthoringUtility.AddExpressionOverride(_expressionData);

            Assert.That(expressionOverride.gameObject, Is.SameAs(_gameObject));
            Assert.That(_expressionData.DataReferences, Is.EqualTo(new ExpressionDataSourceComponent[] { baseData, expressionOverride }));
            Assert.That(expressionOverride.TargetBase, Is.SameAs(baseData));
            Assert.That(_expressionData.Mode, Is.EqualTo(ExpressionDataMode.Reference));
        }

        [Test]
        public void AddExpressionOverrideDoesNotSetTargetBaseWhenThereAreNoBases()
        {
            var expressionOverride = ExpressionDataAuthoringUtility.AddExpressionOverride(_expressionData);

            Assert.That(_expressionData.DataReferences, Is.EqualTo(new ExpressionDataSourceComponent[] { expressionOverride }));
            Assert.That(expressionOverride.TargetBase, Is.Null);
            Assert.That(_expressionData.Mode, Is.EqualTo(ExpressionDataMode.Reference));
        }

        [Test]
        public void AddExpressionOverrideDoesNotSetTargetBaseWhenThereAreMultipleBases()
        {
            var firstBase = ExpressionDataAuthoringUtility.AddBaseData(_expressionData);
            var secondBase = ExpressionDataAuthoringUtility.AddBaseData(_expressionData);

            var expressionOverride = ExpressionDataAuthoringUtility.AddExpressionOverride(_expressionData);

            Assert.That(_expressionData.DataReferences, Is.EqualTo(new ExpressionDataSourceComponent[] { firstBase, secondBase, expressionOverride }));
            Assert.That(expressionOverride.TargetBase, Is.Null);
            Assert.That(_expressionData.Mode, Is.EqualTo(ExpressionDataMode.Reference));
        }

        [Test]
        public void ConvertInlineToReferenceCopiesInlineFieldsToBaseDataWithoutClearingInlineFields()
        {
            var clip = new AnimationClip { name = "Smile" };
            var animation = new BlendShapeWeightAnimation("Smile", AnimationCurve.Constant(0, 1, 100));
            _expressionData.Clip = clip;
            _expressionData.ClipOption = ClipImportOption.All;
            _expressionData.BlendShapeAnimations.Add(animation);
            _expressionData.AllBlendShapeAnimationAsFacial = true;

            var baseData = ExpressionDataAuthoringUtility.ConvertInlineToReference(_expressionData);

            Assert.That(_expressionData.Mode, Is.EqualTo(ExpressionDataMode.Reference));
            Assert.That(_expressionData.DataReferences, Is.EqualTo(new ExpressionDataSourceComponent[] { baseData }));
            Assert.That(baseData.Clip, Is.SameAs(clip));
            Assert.That(baseData.ClipOption, Is.EqualTo(ClipImportOption.All));
            Assert.That(baseData.BlendShapeAnimations, Is.EqualTo(new[] { animation }));
            Assert.That(baseData.AllBlendShapeAnimationAsFacial, Is.True);
            Assert.That(_expressionData.Clip, Is.SameAs(clip));
            Assert.That(_expressionData.BlendShapeAnimations, Is.EqualTo(new[] { animation }));
        }

        [Test]
        public void ExpressionDataSourceMemoIsSerializedOnBaseAndOverride()
        {
            var baseData = _gameObject.AddComponent<BaseExpressionDataComponent>();
            var expressionOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();

            baseData.Memo = "Base memo";
            expressionOverride.Memo = "Override memo";

            var baseSerializedObject = new SerializedObject(baseData);
            var overrideSerializedObject = new SerializedObject(expressionOverride);

            Assert.That(baseSerializedObject.FindProperty(nameof(ExpressionDataSourceComponent.Memo)).stringValue, Is.EqualTo("Base memo"));
            Assert.That(overrideSerializedObject.FindProperty(nameof(ExpressionDataSourceComponent.Memo)).stringValue, Is.EqualTo("Override memo"));
        }

        [Test]
        public void ReferenceModeIgnoresExpressionDataSourceMemo()
        {
            var baseData = _gameObject.AddComponent<BaseExpressionDataComponent>();
            baseData.Memo = "This is only an authoring note.";
            baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
            var expressionOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            expressionOverride.Memo = "This note must not affect animation output.";
            expressionOverride.TargetBase = baseData;
            expressionOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Override", AnimationCurve.Constant(0, 1, 30)));
            _expressionData.Mode = ExpressionDataMode.Reference;
            _expressionData.DataReferences.Add(baseData);

            var result = new List<BlendShapeWeightAnimation>();
            _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

            Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "Override" }));
        }

        [Test]
        public void ReferenceModeAppliesOverrideTargetBaseWhenOnlyOverrideIsReferenced()
        {
            var baseData = _gameObject.AddComponent<BaseExpressionDataComponent>();
            baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
            var expressionOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            expressionOverride.TargetBase = baseData;
            expressionOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Override", AnimationCurve.Constant(0, 1, 30)));
            _expressionData.Mode = ExpressionDataMode.Reference;
            _expressionData.DataReferences.Add(expressionOverride);

            var result = new List<BlendShapeWeightAnimation>();
            _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

            Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "Override" }));
        }

        [Test]
        public void ReferenceModeAppliesBaseAgainWhenBaseAndOverrideAreBothReferenced()
        {
            var baseData = _gameObject.AddComponent<BaseExpressionDataComponent>();
            baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
            var expressionOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            expressionOverride.TargetBase = baseData;
            expressionOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Override", AnimationCurve.Constant(0, 1, 30)));
            _expressionData.Mode = ExpressionDataMode.Reference;
            _expressionData.DataReferences.AddRange(new ExpressionDataSourceComponent[] { baseData, expressionOverride });

            var result = new List<BlendShapeWeightAnimation>();
            _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

            Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "Override" }));
        }

        [Test]
        public void ReferenceModeComplementsUnreferencedSourceOnSameGameObject()
        {
            var baseData = _gameObject.AddComponent<BaseExpressionDataComponent>();
            baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
            var expressionOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            expressionOverride.TargetBase = baseData;
            expressionOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Override", AnimationCurve.Constant(0, 1, 30)));
            _expressionData.Mode = ExpressionDataMode.Reference;
            _expressionData.DataReferences.Add(baseData);

            var result = new List<BlendShapeWeightAnimation>();
            _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

            Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "Override" }));
        }

        [Test]
        public void ReferenceModeAppliesOverrideOnReferencedBaseGameObject()
        {
            var baseGameObject = new GameObject("Base");
            try
            {
                var baseData = baseGameObject.AddComponent<BaseExpressionDataComponent>();
                baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
                var expressionOverride = baseGameObject.AddComponent<ExpressionOverrideComponent>();
                expressionOverride.TargetBase = baseData;
                expressionOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Override", AnimationCurve.Constant(0, 1, 30)));
                _expressionData.Mode = ExpressionDataMode.Reference;
                _expressionData.DataReferences.Add(baseData);

                var result = new List<BlendShapeWeightAnimation>();
                _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

                Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "Override" }));
            }
            finally
            {
                Object.DestroyImmediate(baseGameObject);
            }
        }

        [Test]
        public void ReferenceModeAppliesSameGameObjectOverridesInComponentOrder()
        {
            var baseData = _gameObject.AddComponent<BaseExpressionDataComponent>();
            baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
            var firstOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            firstOverride.TargetBase = baseData;
            firstOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("FirstOverride", AnimationCurve.Constant(0, 1, 20)));
            var secondOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            secondOverride.TargetBase = baseData;
            secondOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("SecondOverride", AnimationCurve.Constant(0, 1, 30)));
            _expressionData.Mode = ExpressionDataMode.Reference;
            _expressionData.DataReferences.Add(baseData);

            var result = new List<BlendShapeWeightAnimation>();
            _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

            Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "FirstOverride", "SecondOverride" }));
        }

        [Test]
        public void ReferenceModeAppliesOverrideTargetingOverrideOnAnotherGameObject()
        {
            var baseObject = new GameObject("Base");
            var overrideObject = new GameObject("Override");
            try
            {
                var baseData = baseObject.AddComponent<BaseExpressionDataComponent>();
                baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
                var firstOverride = baseObject.AddComponent<ExpressionOverrideComponent>();
                firstOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("FirstOverride", AnimationCurve.Constant(0, 1, 20)));
                var secondOverride = overrideObject.AddComponent<ExpressionOverrideComponent>();
                secondOverride.TargetBase = firstOverride;
                secondOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("SecondOverride", AnimationCurve.Constant(0, 1, 30)));
                _expressionData.Mode = ExpressionDataMode.Reference;
                _expressionData.DataReferences.Add(secondOverride);

                var result = new List<BlendShapeWeightAnimation>();
                _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

                Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "FirstOverride", "SecondOverride" }));
            }
            finally
            {
                Object.DestroyImmediate(baseObject);
                Object.DestroyImmediate(overrideObject);
            }
        }

        [Test]
        public void ReferenceModeIgnoresSameGameObjectTargetAndUsesComponentOrder()
        {
            var baseData = _gameObject.AddComponent<BaseExpressionDataComponent>();
            baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
            var firstOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            firstOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("FirstOverride", AnimationCurve.Constant(0, 1, 20)));
            var secondOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            secondOverride.TargetBase = firstOverride;
            secondOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("SecondOverride", AnimationCurve.Constant(0, 1, 30)));
            _expressionData.Mode = ExpressionDataMode.Reference;
            _expressionData.DataReferences.Add(baseData);

            var result = new List<BlendShapeWeightAnimation>();
            _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

            Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "FirstOverride", "SecondOverride" }));
        }

        [Test]
        public void ReferenceModeSkipsOverrideOnlySameGameObjectStackWhenTargetBaseIsNull()
        {
            var firstOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            firstOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("FirstOverride", AnimationCurve.Constant(0, 1, 20)));
            var secondOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            secondOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("SecondOverride", AnimationCurve.Constant(0, 1, 30)));
            _expressionData.Mode = ExpressionDataMode.Reference;
            _expressionData.DataReferences.Add(firstOverride);

            var result = new List<BlendShapeWeightAnimation>();
            _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ReferenceModeAppliesFirstOverrideOnlyStackOverrideFromTarget()
        {
            var baseObject = new GameObject("Base");
            var overrideObject = new GameObject("Override");
            try
            {
                var baseData = baseObject.AddComponent<BaseExpressionDataComponent>();
                baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
                var firstOverride = overrideObject.AddComponent<ExpressionOverrideComponent>();
                firstOverride.TargetBase = baseData;
                firstOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("FirstOverride", AnimationCurve.Constant(0, 1, 20)));
                var secondOverride = overrideObject.AddComponent<ExpressionOverrideComponent>();
                secondOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("SecondOverride", AnimationCurve.Constant(0, 1, 30)));
                _expressionData.Mode = ExpressionDataMode.Reference;
                _expressionData.DataReferences.Add(firstOverride);

                var result = new List<BlendShapeWeightAnimation>();
                _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

                Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "FirstOverride" }));
            }
            finally
            {
                Object.DestroyImmediate(baseObject);
                Object.DestroyImmediate(overrideObject);
            }
        }

        [Test]
        public void ReferenceModeAppliesOverrideOnlyStackThroughDirectChildReference()
        {
            var baseObject = new GameObject("Base");
            var overrideObject = new GameObject("Override");
            try
            {
                var baseData = baseObject.AddComponent<BaseExpressionDataComponent>();
                baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
                var firstOverride = overrideObject.AddComponent<ExpressionOverrideComponent>();
                firstOverride.TargetBase = baseData;
                firstOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("FirstOverride", AnimationCurve.Constant(0, 1, 20)));
                var secondOverride = overrideObject.AddComponent<ExpressionOverrideComponent>();
                secondOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("SecondOverride", AnimationCurve.Constant(0, 1, 30)));
                _expressionData.Mode = ExpressionDataMode.Reference;
                _expressionData.DataReferences.Add(secondOverride);

                var result = new List<BlendShapeWeightAnimation>();
                _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

                Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "FirstOverride", "SecondOverride" }));
            }
            finally
            {
                Object.DestroyImmediate(baseObject);
                Object.DestroyImmediate(overrideObject);
            }
        }

        [Test]
        public void ReferenceModeDoesNotDuplicateOverrideOnlyStackWhenMultipleOverridesAreReferenced()
        {
            var baseObject = new GameObject("Base");
            var overrideObject = new GameObject("Override");
            try
            {
                var baseData = baseObject.AddComponent<BaseExpressionDataComponent>();
                baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
                var firstOverride = overrideObject.AddComponent<ExpressionOverrideComponent>();
                firstOverride.TargetBase = baseData;
                firstOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("FirstOverride", AnimationCurve.Constant(0, 1, 20)));
                var secondOverride = overrideObject.AddComponent<ExpressionOverrideComponent>();
                secondOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("SecondOverride", AnimationCurve.Constant(0, 1, 30)));
                _expressionData.Mode = ExpressionDataMode.Reference;
                _expressionData.DataReferences.AddRange(new ExpressionDataSourceComponent[] { firstOverride, secondOverride });

                var result = new List<BlendShapeWeightAnimation>();
                _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

                Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "FirstOverride", "SecondOverride" }));
            }
            finally
            {
                Object.DestroyImmediate(baseObject);
                Object.DestroyImmediate(overrideObject);
            }
        }

        [Test]
        public void ReferenceModeSkipsCircularOverrideTargetChain()
        {
            var firstObject = new GameObject("First");
            var secondObject = new GameObject("Second");
            try
            {
                var firstOverride = firstObject.AddComponent<ExpressionOverrideComponent>();
                firstOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("FirstOverride", AnimationCurve.Constant(0, 1, 20)));
                var secondOverride = secondObject.AddComponent<ExpressionOverrideComponent>();
                secondOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("SecondOverride", AnimationCurve.Constant(0, 1, 30)));
                firstOverride.TargetBase = secondOverride;
                secondOverride.TargetBase = firstOverride;
                _expressionData.Mode = ExpressionDataMode.Reference;
                _expressionData.DataReferences.Add(firstOverride);

                var result = new List<BlendShapeWeightAnimation>();
                _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

                Assert.That(result, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void ReferenceModeSkipsOverrideWhenTargetBaseIsNull()
        {
            var expressionOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            expressionOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Override", AnimationCurve.Constant(0, 1, 30)));
            _expressionData.Mode = ExpressionDataMode.Reference;
            _expressionData.DataReferences.Add(expressionOverride);

            var result = new List<BlendShapeWeightAnimation>();
            _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ReferenceModeAppliesZeroBlendShapeOverride()
        {
            var baseData = _gameObject.AddComponent<BaseExpressionDataComponent>();
            baseData.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("Base", AnimationCurve.Constant(0, 1, 10)));
            var expressionOverride = _gameObject.AddComponent<ExpressionOverrideComponent>();
            expressionOverride.TargetBase = baseData;
            expressionOverride.BlendShapeAnimations.Add(new BlendShapeWeightAnimation("ZeroReset", AnimationCurve.Constant(0, 1, 0)));
            _expressionData.Mode = ExpressionDataMode.Reference;
            _expressionData.DataReferences.Add(expressionOverride);

            var result = new List<BlendShapeWeightAnimation>();
            _expressionData.GetBlendShapeAnimations(result, new List<BlendShapeWeightAnimation>(), "Body");

            Assert.That(result.Select(animation => animation.Name).ToArray(), Is.EqualTo(new[] { "Base", "ZeroReset" }));
            Assert.That(result.Last().ToFirstFrameBlendShape().Weight, Is.EqualTo(0f));
        }
    }
}
