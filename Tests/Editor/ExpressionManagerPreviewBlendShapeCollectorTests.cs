using Aoyon.FaceTune.Gui;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Aoyon.FaceTune.Tests
{
    public class ExpressionManagerPreviewBlendShapeCollectorTests
    {
        private GameObject _avatarRoot = null!;
        private GameObject _faceObject = null!;
        private SkinnedMeshRenderer _faceRenderer = null!;
        private Mesh _faceMesh = null!;
        private AvatarContext _avatarContext = null!;

        [SetUp]
        public void SetUp()
        {
            _avatarRoot = new GameObject("Avatar");
            _faceObject = CreateChild(_avatarRoot, "Body");
            _faceRenderer = _faceObject.AddComponent<SkinnedMeshRenderer>();
            _faceMesh = CreateFaceMesh("Smile", "Blink", "Style", "Base", "Override");
            _faceRenderer.sharedMesh = _faceMesh;

            var zeroShapes = new BlendShapeWeightSet(new[]
            {
                new BlendShapeWeight("Smile", 0),
                new BlendShapeWeight("Blink", 0),
                new BlendShapeWeight("Style", 0),
                new BlendShapeWeight("Base", 0),
                new BlendShapeWeight("Override", 0),
            });

            _avatarContext = new AvatarContext(
                _avatarRoot,
                _faceRenderer,
                _faceMesh,
                "Body",
                zeroShapes,
                new HashSet<string>(),
                zeroShapes);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_faceMesh);
            Object.DestroyImmediate(_avatarRoot);
        }

        [Test]
        public void CollectMergesExpressionDataInHierarchyOrder()
        {
            var expression = CreateExpression(_avatarRoot, "SmileExpression");
            AddExpressionData(expression, "Smile", 10);
            AddExpressionData(expression, "Smile", 60);

            var result = ExpressionManagerPreviewBlendShapeCollector.Collect(expression, _avatarContext);

            Assert.That(result.TryGetValue("Smile", out var shape), Is.True);
            Assert.That(shape.Weight, Is.EqualTo(60));
        }

        [Test]
        public void CollectAddsSafeZeroShapesWhenExpressionDoesNotEnableBlending()
        {
            var expression = CreateExpression(_avatarRoot, "SmileExpression");
            expression.FacialSettings = expression.FacialSettings with { EnableBlending = false };
            AddExpressionData(expression, "Smile", 40);

            var result = ExpressionManagerPreviewBlendShapeCollector.Collect(expression, _avatarContext);

            Assert.That(result.TryGetValue("Smile", out var smile), Is.True);
            Assert.That(smile.Weight, Is.EqualTo(40));
            Assert.That(result.TryGetValue("Blink", out var blink), Is.True);
            Assert.That(blink.Weight, Is.EqualTo(0));
        }

        [Test]
        public void CollectIncludesFacialStyleBeforeExpressionData()
        {
            var styleObject = CreateChild(_avatarRoot, "StyleRoot");
            var facialStyle = styleObject.AddComponent<FacialStyleComponent>();
            facialStyle.BlendShapeAnimations.Add(BlendShapeWeightAnimation.SingleFrame("Style", 25));
            var expression = CreateExpression(styleObject, "SmileExpression");
            AddExpressionData(expression, "Smile", 40);

            var result = ExpressionManagerPreviewBlendShapeCollector.Collect(expression, _avatarContext);

            Assert.That(result.TryGetValue("Style", out var style), Is.True);
            Assert.That(style.Weight, Is.EqualTo(25));
            Assert.That(result.TryGetValue("Smile", out var smile), Is.True);
            Assert.That(smile.Weight, Is.EqualTo(40));
        }

        [Test]
        public void CollectExpandsReferencedBaseAndOverrideData()
        {
            var expression = CreateExpression(_avatarRoot, "SmileExpression");
            var expressionData = expression.gameObject.AddComponent<ExpressionDataComponent>();
            var baseData = expression.gameObject.AddComponent<BaseExpressionDataComponent>();
            var expressionOverride = expression.gameObject.AddComponent<ExpressionOverrideComponent>();
            expressionData.Mode = ExpressionDataMode.Reference;
            expressionData.DataReferences.Add(baseData);
            baseData.BlendShapeAnimations.Add(BlendShapeWeightAnimation.SingleFrame("Base", 20));
            expressionOverride.TargetBase = baseData;
            expressionOverride.BlendShapeAnimations.Add(BlendShapeWeightAnimation.SingleFrame("Override", 70));

            var result = ExpressionManagerPreviewBlendShapeCollector.Collect(expression, _avatarContext);

            Assert.That(result.TryGetValue("Base", out var baseShape), Is.True);
            Assert.That(baseShape.Weight, Is.EqualTo(20));
            Assert.That(result.TryGetValue("Override", out var overrideShape), Is.True);
            Assert.That(overrideShape.Weight, Is.EqualTo(70));
        }

        [Test]
        public void CollectSourceIncludesOwnBlendShapes()
        {
            var sourceObject = CreateChild(_avatarRoot, "Source");
            var baseData = sourceObject.AddComponent<BaseExpressionDataComponent>();
            baseData.BlendShapeAnimations.Add(BlendShapeWeightAnimation.SingleFrame("Base", 20));

            var result = ExpressionManagerPreviewBlendShapeCollector.Collect(baseData, _avatarContext);

            Assert.That(result.TryGetValue("Base", out var baseShape), Is.True);
            Assert.That(baseShape.Weight, Is.EqualTo(20));
        }

        [Test]
        public void CollectOverrideSourceIncludesBaseSourcesBeforeOverride()
        {
            var sourceObject = CreateChild(_avatarRoot, "Source");
            var baseData = sourceObject.AddComponent<BaseExpressionDataComponent>();
            var expressionOverride = sourceObject.AddComponent<ExpressionOverrideComponent>();
            baseData.BlendShapeAnimations.Add(BlendShapeWeightAnimation.SingleFrame("Base", 20));
            expressionOverride.BlendShapeAnimations.Add(BlendShapeWeightAnimation.SingleFrame("Override", 70));

            var result = ExpressionManagerPreviewBlendShapeCollector.Collect(expressionOverride, _avatarContext);

            Assert.That(result.TryGetValue("Base", out var baseShape), Is.True);
            Assert.That(baseShape.Weight, Is.EqualTo(20));
            Assert.That(result.TryGetValue("Override", out var overrideShape), Is.True);
            Assert.That(overrideShape.Weight, Is.EqualTo(70));
        }

        private static ExpressionDataComponent AddExpressionData(ExpressionComponent expression, string shapeName, float weight)
        {
            var expressionData = expression.gameObject.AddComponent<ExpressionDataComponent>();
            expressionData.BlendShapeAnimations.Add(BlendShapeWeightAnimation.SingleFrame(shapeName, weight));
            return expressionData;
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            return child;
        }

        private static ExpressionComponent CreateExpression(GameObject parent, string name)
        {
            var expressionObject = CreateChild(parent, name);
            return expressionObject.AddComponent<ExpressionComponent>();
        }

        private static Mesh CreateFaceMesh(params string[] blendShapeNames)
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
                normals = new[] { Vector3.back, Vector3.back, Vector3.back },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up },
            };

            var deltaVertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            var deltaNormals = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            var deltaTangents = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            foreach (var blendShapeName in blendShapeNames)
            {
                mesh.AddBlendShapeFrame(blendShapeName, 100, deltaVertices, deltaNormals, deltaTangents);
            }

            return mesh;
        }
    }
}
