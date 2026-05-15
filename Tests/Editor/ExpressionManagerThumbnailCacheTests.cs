using Aoyon.FaceTune.Gui;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aoyon.FaceTune.Tests
{
    public class ExpressionManagerThumbnailCacheTests
    {
        private GameObject _avatarRoot = null!;
        private GameObject _expressionObject = null!;
        private GameObject _faceObject = null!;
        private SkinnedMeshRenderer _faceRenderer = null!;
        private Mesh _faceMesh = null!;
        private AvatarContext _avatarContext = null!;
        private ExpressionManagerExpressionItem _item = null!;
        private ExpressionManagerUnlinkedSourceItem _unlinkedSourceItem = null!;
        private readonly Queue<Action> _delayCalls = new();

        [SetUp]
        public void SetUp()
        {
            _delayCalls.Clear();
            _avatarRoot = new GameObject("Avatar");
            _faceObject = CreateChild(_avatarRoot, "Body");
            _faceRenderer = _faceObject.AddComponent<SkinnedMeshRenderer>();
            _faceMesh = CreateFaceMesh();
            _faceRenderer.sharedMesh = _faceMesh;

            var zeroShapes = new BlendShapeWeightSet(new[] { new BlendShapeWeight("Smile", 0) });
            _avatarContext = new AvatarContext(
                _avatarRoot,
                _faceRenderer,
                _faceMesh,
                "Body",
                zeroShapes,
                new HashSet<string>(),
                zeroShapes);

            _expressionObject = CreateChild(_avatarRoot, "Smile");
            var expression = _expressionObject.AddComponent<ExpressionComponent>();
            var expressionData = _expressionObject.AddComponent<ExpressionDataComponent>();
            expressionData.BlendShapeAnimations.Add(BlendShapeWeightAnimation.SingleFrame("Smile", 100));
            _item = new ExpressionManagerExpressionItem(
                _avatarRoot,
                expression,
                "Smile",
                new[] { expressionData },
                new Component[] { expressionData },
                0);

            var library = CreateChild(_avatarRoot, "Library");
            var baseData = library.AddComponent<BaseExpressionDataComponent>();
            baseData.BlendShapeAnimations.Add(BlendShapeWeightAnimation.SingleFrame("Smile", 80));
            _unlinkedSourceItem = new ExpressionManagerUnlinkedSourceItem(baseData, "Library");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_faceMesh);
            UnityEngine.Object.DestroyImmediate(_avatarRoot);
        }

        [Test]
        public void GetOrRequestQueuesThumbnailAndReturnsCachedTextureAfterDelayCall()
        {
            var renderCount = 0;
            var repaintCount = 0;
            var cache = CreateCache((_, _, _) =>
            {
                renderCount++;
                return CreateTexture();
            });

            var first = cache.GetOrRequest(_item, _avatarContext, "renderer-state", () => repaintCount++);
            RunNextDelayCall();
            var second = cache.GetOrRequest(_item, _avatarContext, "renderer-state", () => repaintCount++);

            Assert.That(first.Status, Is.EqualTo(ExpressionManagerThumbnailStatus.Queued));
            Assert.That(second.Status, Is.EqualTo(ExpressionManagerThumbnailStatus.Ready));
            Assert.That(second.Texture, Is.Not.Null);
            Assert.That(renderCount, Is.EqualTo(1));
            Assert.That(repaintCount, Is.EqualTo(1));

            cache.Dispose();
        }

        [Test]
        public void GetOrRequestDoesNotRenderAgainForCacheHit()
        {
            var renderCount = 0;
            var cache = CreateCache((_, _, _) =>
            {
                renderCount++;
                return CreateTexture();
            });

            cache.GetOrRequest(_item, _avatarContext, "renderer-state", () => { });
            RunNextDelayCall();
            cache.GetOrRequest(_item, _avatarContext, "renderer-state", () => { });
            cache.GetOrRequest(_item, _avatarContext, "renderer-state", () => { });

            Assert.That(renderCount, Is.EqualTo(1));

            cache.Dispose();
        }

        [Test]
        public void FailedThumbnailIsCachedAndDoesNotRetryEveryRepaint()
        {
            var renderCount = 0;
            var cache = CreateCache((_, _, _) =>
            {
                renderCount++;
                return null;
            });

            cache.GetOrRequest(_item, _avatarContext, "renderer-state", () => { });
            RunNextDelayCall();
            var failed = cache.GetOrRequest(_item, _avatarContext, "renderer-state", () => { });
            var failedAgain = cache.GetOrRequest(_item, _avatarContext, "renderer-state", () => { });

            Assert.That(failed.Status, Is.EqualTo(ExpressionManagerThumbnailStatus.Failed));
            Assert.That(failedAgain.Status, Is.EqualTo(ExpressionManagerThumbnailStatus.Failed));
            Assert.That(renderCount, Is.EqualTo(1));

            cache.Dispose();
        }

        [Test]
        public void ClearRemovesCachedEntryAndAllowsRegeneration()
        {
            var renderCount = 0;
            var cache = CreateCache((_, _, _) =>
            {
                renderCount++;
                return CreateTexture();
            });

            cache.GetOrRequest(_item, _avatarContext, "renderer-state", () => { });
            RunNextDelayCall();
            cache.Clear();
            cache.GetOrRequest(_item, _avatarContext, "renderer-state", () => { });
            RunNextDelayCall();

            Assert.That(renderCount, Is.EqualTo(2));

            cache.Dispose();
        }

        [Test]
        public void RendererStateChangeInvalidatesCachedThumbnail()
        {
            var renderCount = 0;
            var cache = CreateCache((_, _, _) =>
            {
                renderCount++;
                return CreateTexture();
            });

            cache.GetOrRequest(_item, _avatarContext, "renderer-state-a", () => { });
            RunNextDelayCall();
            cache.GetOrRequest(_item, _avatarContext, "renderer-state-b", () => { });
            RunNextDelayCall();

            Assert.That(renderCount, Is.EqualTo(2));

            cache.Dispose();
        }

        [Test]
        public void GetOrRequestQueuesUnlinkedSourceThumbnail()
        {
            var renderCount = 0;
            var cache = CreateCache((_, blendShapes, _) =>
            {
                renderCount++;
                Assert.That(blendShapes.TryGetValue("Smile", out var shape), Is.True);
                Assert.That(shape.Weight, Is.EqualTo(80));
                return CreateTexture();
            });

            var first = cache.GetOrRequest(_unlinkedSourceItem, _avatarContext, "renderer-state", () => { });
            RunNextDelayCall();
            var second = cache.GetOrRequest(_unlinkedSourceItem, _avatarContext, "renderer-state", () => { });

            Assert.That(first.Status, Is.EqualTo(ExpressionManagerThumbnailStatus.Queued));
            Assert.That(second.Status, Is.EqualTo(ExpressionManagerThumbnailStatus.Ready));
            Assert.That(renderCount, Is.EqualTo(1));

            cache.Dispose();
        }

        private ExpressionManagerThumbnailCache CreateCache(
            Func<AvatarContext, IReadOnlyBlendShapeSet, int, Texture2D?> renderThumbnail)
        {
            return new ExpressionManagerThumbnailCache(renderThumbnail, action => _delayCalls.Enqueue(action));
        }

        private void RunNextDelayCall()
        {
            Assert.That(_delayCalls.Count, Is.GreaterThan(0));
            var action = _delayCalls.Dequeue();
            action();
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            return child;
        }

        private static Mesh CreateFaceMesh()
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
            mesh.AddBlendShapeFrame("Smile", 100, deltaVertices, deltaNormals, deltaTangents);
            return mesh;
        }

        private static Texture2D CreateTexture()
        {
            var texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return texture;
        }
    }
}
