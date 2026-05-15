using Aoyon.FaceTune.Gui;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Aoyon.FaceTune.Tests
{
    public class ExpressionManagerThumbnailRendererTests
    {
        private GameObject _avatarRoot = null!;
        private GameObject _faceObject = null!;
        private SkinnedMeshRenderer _faceRenderer = null!;
        private Mesh _faceMesh = null!;
        private Material _material = null!;
        private AvatarContext _avatarContext = null!;

        [SetUp]
        public void SetUp()
        {
            _avatarRoot = new GameObject("Avatar");
            _faceObject = new GameObject("Body");
            _faceObject.transform.SetParent(_avatarRoot.transform);
            _faceRenderer = _faceObject.AddComponent<SkinnedMeshRenderer>();
            _faceMesh = CreateFaceMesh();
            _material = new Material(Shader.Find("Standard"));
            _faceRenderer.sharedMesh = _faceMesh;
            _faceRenderer.sharedMaterial = _material;
            _faceRenderer.SetBlendShapeWeight(0, 15);

            var zeroShapes = new BlendShapeWeightSet(new[] { new BlendShapeWeight("Smile", 0) });
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
            Object.DestroyImmediate(_material);
            Object.DestroyImmediate(_faceMesh);
            Object.DestroyImmediate(_avatarRoot);
        }

        [Test]
        public void RenderRestoresOriginalBlendShapeWeights()
        {
            var renderer = new ExpressionManagerThumbnailRenderer();
            try
            {
                var previewShapes = new BlendShapeWeightSet(new[] { new BlendShapeWeight("Smile", 90) });

                var texture = renderer.Render(_avatarContext, previewShapes, 32);

                if (texture != null) Object.DestroyImmediate(texture);
                Assert.That(_faceRenderer.GetBlendShapeWeight(0), Is.EqualTo(15));
            }
            finally
            {
                renderer.Dispose();
            }
        }

        [Test]
        public void CalculateCameraSettingsPlacesCameraInFrontOfFaceAndLooksAtUpperCenter()
        {
            var bounds = new Bounds(new Vector3(0, 1, 0), new Vector3(1, 2, 0.5f));

            var settings = ExpressionManagerThumbnailRenderer.CalculateCameraSettings(bounds);

            Assert.That(settings.Position.z, Is.GreaterThan(bounds.center.z));
            Assert.That(settings.Target.y, Is.GreaterThan(bounds.center.y));
            Assert.That(Vector3.Dot(settings.Rotation * Vector3.forward, (settings.Target - settings.Position).normalized), Is.GreaterThan(0.999f));
            Assert.That(settings.FieldOfView, Is.LessThan(30f));
        }

        [Test]
        public void CollectRenderableRenderersIncludesVisibleNonFaceMeshes()
        {
            var hairObject = new GameObject("Hair");
            hairObject.transform.SetParent(_avatarRoot.transform);
            var hairMeshFilter = hairObject.AddComponent<MeshFilter>();
            hairMeshFilter.sharedMesh = _faceMesh;
            var hairRenderer = hairObject.AddComponent<MeshRenderer>();
            hairRenderer.sharedMaterial = _material;

            var disabledObject = new GameObject("Disabled");
            disabledObject.transform.SetParent(_avatarRoot.transform);
            disabledObject.AddComponent<MeshFilter>().sharedMesh = _faceMesh;
            disabledObject.AddComponent<MeshRenderer>().enabled = false;

            var renderers = ExpressionManagerThumbnailRenderer
                .CollectRenderableRenderers(_avatarRoot)
                .ToArray();

            Assert.That(renderers, Does.Contain(_faceRenderer));
            Assert.That(renderers, Does.Contain(hairRenderer));
            Assert.That(renderers.Select(renderer => renderer.name), Does.Not.Contain("Disabled"));
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

            var deltaVertices = new[] { Vector3.zero, Vector3.forward, Vector3.zero };
            var deltaNormals = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            var deltaTangents = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            mesh.AddBlendShapeFrame("Smile", 100, deltaVertices, deltaNormals, deltaTangents);
            return mesh;
        }
    }
}
