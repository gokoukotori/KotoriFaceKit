namespace Aoyon.FaceTune.Gui;

internal sealed class ExpressionManagerThumbnailRenderer : IDisposable
{
    private readonly PreviewRenderUtility _previewRenderUtility = new();
    internal readonly struct ThumbnailCameraSettings
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Target;
        public readonly float NearClipPlane;
        public readonly float FarClipPlane;
        public readonly float FieldOfView;

        public ThumbnailCameraSettings(
            Vector3 position,
            Quaternion rotation,
            Vector3 target,
            float nearClipPlane,
            float farClipPlane,
            float fieldOfView)
        {
            Position = position;
            Rotation = rotation;
            Target = target;
            NearClipPlane = nearClipPlane;
            FarClipPlane = farClipPlane;
            FieldOfView = fieldOfView;
        }
    }

    public Texture2D? Render(AvatarContext avatarContext, IReadOnlyBlendShapeSet blendShapes, int size)
    {
        var previewMeshes = new List<PreviewMesh>();
        var ownedMeshes = new List<Mesh>();

        try
        {
            Bounds? cameraBounds = null;
            foreach (var renderer in CollectRenderableRenderers(avatarContext.Root))
            {
                if (!TryCreatePreviewMesh(
                        renderer,
                        avatarContext.Root.transform,
                        avatarContext.FaceRenderer,
                        blendShapes,
                        out var previewMesh,
                        out var ownedMesh))
                {
                    continue;
                }

                previewMeshes.Add(previewMesh);
                if (ownedMesh != null)
                {
                    ownedMeshes.Add(ownedMesh);
                }

                var bounds = CalculateTransformedBounds(previewMesh.Mesh.bounds, previewMesh.Matrix);
                if (renderer == avatarContext.FaceRenderer)
                {
                    cameraBounds = bounds;
                }
            }

            Bounds resolvedBounds;
            if (cameraBounds.HasValue)
            {
                resolvedBounds = cameraBounds.Value;
            }
            else if (TryCalculateBounds(previewMeshes, out var previewBounds))
            {
                resolvedBounds = previewBounds;
            }
            else
            {
                return null;
            }

            SetupCamera(resolvedBounds);

            _previewRenderUtility.BeginStaticPreview(new Rect(0, 0, size, size));
            foreach (var previewMesh in previewMeshes)
            {
                DrawPreviewMesh(previewMesh);
            }

            _previewRenderUtility.camera.Render();
            var texture = _previewRenderUtility.EndStaticPreview();
            if (texture != null)
            {
                texture.hideFlags = HideFlags.HideAndDontSave;
            }
            return texture;
        }
        catch
        {
            return null;
        }
        finally
        {
            foreach (var ownedMesh in ownedMeshes)
            {
                Object.DestroyImmediate(ownedMesh);
            }
        }
    }

    internal static IEnumerable<Renderer> CollectRenderableRenderers(GameObject avatarRoot)
    {
        return avatarRoot
            .GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            .Where(HasRenderableMesh);
    }

    private static bool HasRenderableMesh(Renderer renderer)
    {
        return renderer switch
        {
            SkinnedMeshRenderer skinnedMeshRenderer => skinnedMeshRenderer.sharedMesh != null,
            MeshRenderer meshRenderer => meshRenderer.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null,
            _ => false
        };
    }

    private static bool TryCreatePreviewMesh(
        Renderer sourceRenderer,
        Transform sourceRoot,
        SkinnedMeshRenderer faceRenderer,
        IReadOnlyBlendShapeSet faceBlendShapes,
        out PreviewMesh previewMesh,
        out Mesh? ownedMesh)
    {
        previewMesh = default;
        ownedMesh = null;

        var mesh = sourceRenderer switch
        {
            SkinnedMeshRenderer skinnedMeshRenderer => BakeRendererMesh(skinnedMeshRenderer, faceRenderer, faceBlendShapes),
            MeshRenderer meshRenderer => GetStaticMesh(meshRenderer),
            _ => null
        };

        if (mesh == null) return false;

        if (sourceRenderer is SkinnedMeshRenderer)
        {
            ownedMesh = mesh;
        }

        previewMesh = new PreviewMesh(
            mesh,
            CreateRelativeMatrix(sourceRenderer.transform, sourceRoot),
            sourceRenderer.sharedMaterials);
        return true;
    }

    private static Mesh? BakeRendererMesh(
        SkinnedMeshRenderer sourceRenderer,
        SkinnedMeshRenderer faceRenderer,
        IReadOnlyBlendShapeSet faceBlendShapes)
    {
        var mesh = sourceRenderer.sharedMesh;
        if (mesh == null) return null;

        var bakedMesh = new Mesh
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = $"FaceTune Expression Thumbnail Mesh {sourceRenderer.name}"
        };

        if (sourceRenderer == faceRenderer)
        {
            BakePreviewMesh(sourceRenderer, mesh, faceBlendShapes, bakedMesh);
        }
        else
        {
            sourceRenderer.BakeMesh(bakedMesh);
        }

        return bakedMesh;
    }

    private static Mesh? GetStaticMesh(MeshRenderer meshRenderer)
    {
        return meshRenderer.TryGetComponent<MeshFilter>(out var meshFilter) ? meshFilter.sharedMesh : null;
    }

    private static Matrix4x4 CreateRelativeMatrix(Transform source, Transform sourceRoot)
    {
        return Matrix4x4.TRS(
            sourceRoot.InverseTransformPoint(source.position),
            Quaternion.Inverse(sourceRoot.rotation) * source.rotation,
            DivideScale(source.lossyScale, sourceRoot.lossyScale));
    }

    private static Vector3 DivideScale(Vector3 scale, Vector3 divisor)
    {
        return new Vector3(
            divisor.x == 0 ? scale.x : scale.x / divisor.x,
            divisor.y == 0 ? scale.y : scale.y / divisor.y,
            divisor.z == 0 ? scale.z : scale.z / divisor.z);
    }

    private static bool TryCalculateBounds(IEnumerable<PreviewMesh> previewMeshes, out Bounds bounds)
    {
        bounds = default;
        var initialized = false;
        foreach (var previewMesh in previewMeshes)
        {
            var previewBounds = CalculateTransformedBounds(previewMesh.Mesh.bounds, previewMesh.Matrix);
            if (!initialized)
            {
                bounds = previewBounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(previewBounds);
            }
        }

        return initialized;
    }

    private static Bounds CalculateTransformedBounds(Bounds bounds, Matrix4x4 matrix)
    {
        var center = bounds.center;
        var extents = bounds.extents;
        var initialized = false;
        var transformedBounds = default(Bounds);

        for (var x = -1; x <= 1; x += 2)
        {
            for (var y = -1; y <= 1; y += 2)
            {
                for (var z = -1; z <= 1; z += 2)
                {
                    var point = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    var transformedPoint = matrix.MultiplyPoint3x4(point);
                    if (!initialized)
                    {
                        transformedBounds = new Bounds(transformedPoint, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        transformedBounds.Encapsulate(transformedPoint);
                    }
                }
            }
        }

        return transformedBounds;
    }

    private void DrawPreviewMesh(PreviewMesh previewMesh)
    {
        var subMeshCount = previewMesh.Mesh.subMeshCount;
        for (var i = 0; i < subMeshCount; i++)
        {
            var material = i < previewMesh.Materials.Length ? previewMesh.Materials[i] : null;
            if (material == null) continue;
            _previewRenderUtility.DrawMesh(previewMesh.Mesh, previewMesh.Matrix, material, i);
        }
    }

    private static void BakePreviewMesh(
        SkinnedMeshRenderer originalRenderer,
        Mesh mesh,
        IReadOnlyBlendShapeSet blendShapes,
        Mesh bakedMesh)
    {
        var originalWeights = CaptureBlendShapeWeights(originalRenderer, mesh);
        try
        {
            originalRenderer.ApplyBlendShapes(mesh, blendShapes.Clone(), -1);
            originalRenderer.BakeMesh(bakedMesh);
        }
        finally
        {
            RestoreBlendShapeWeights(originalRenderer, originalWeights);
        }
    }

    private static float[] CaptureBlendShapeWeights(SkinnedMeshRenderer renderer, Mesh mesh)
    {
        var weights = new float[mesh.blendShapeCount];
        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] = renderer.GetBlendShapeWeight(i);
        }
        return weights;
    }

    private static void RestoreBlendShapeWeights(SkinnedMeshRenderer renderer, IReadOnlyList<float> weights)
    {
        for (var i = 0; i < weights.Count; i++)
        {
            renderer.SetBlendShapeWeight(i, weights[i]);
        }
    }

    private void SetupCamera(Bounds bounds)
    {
        var settings = CalculateCameraSettings(bounds);

        var camera = _previewRenderUtility.camera;
        camera.transform.position = settings.Position;
        camera.transform.rotation = settings.Rotation;
        camera.nearClipPlane = settings.NearClipPlane;
        camera.farClipPlane = settings.FarClipPlane;
        camera.fieldOfView = settings.FieldOfView;
        camera.clearFlags = CameraClearFlags.Color;
        camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0f);

        _previewRenderUtility.lights[0].intensity = 1.2f;
        _previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0);
        _previewRenderUtility.lights[1].intensity = 0.8f;
    }

    internal static ThumbnailCameraSettings CalculateCameraSettings(Bounds bounds)
    {
        var target = bounds.center + Vector3.up * bounds.extents.y * 0.08f;
        var fieldOfView = 22f;
        var visibleHeight = Mathf.Max(bounds.size.y * 1.25f, 0.35f);
        var distance = visibleHeight * 0.5f / Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f);
        distance = Mathf.Max(distance, bounds.extents.z + 0.2f);

        var position = target + Vector3.forward * distance;
        var rotation = Quaternion.LookRotation(target - position, Vector3.up);
        var farClipPlane = Mathf.Max(distance + bounds.extents.magnitude * 2f, 5f);
        return new ThumbnailCameraSettings(position, rotation, target, 0.01f, farClipPlane, fieldOfView);
    }

    public void Dispose()
    {
        _previewRenderUtility.Cleanup();
    }

    private readonly struct PreviewMesh
    {
        public Mesh Mesh { get; }
        public Matrix4x4 Matrix { get; }
        public Material[] Materials { get; }

        public PreviewMesh(Mesh mesh, Matrix4x4 matrix, Material[] materials)
        {
            Mesh = mesh;
            Matrix = matrix;
            Materials = materials;
        }
    }
}
