using UnityEngine;
using UnityEngine.Rendering.Universal;

// A modest planar reflection for the archive's horizontal polished inset.
// It renders only nearby, twelve times a second, and never changes shared assets.
public sealed class SignalArchiveReflection : MonoBehaviour
{
    private static readonly int ReflectionTextureId = Shader.PropertyToID("_ReflectionTex");
    private static readonly int ReflectionStrengthId = Shader.PropertyToID("_ReflectionStrength");
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Renderer mirrorSurface;
    [SerializeField] private Material mirrorMaterial;
    private Camera reflectionCamera;
    private RenderTexture reflectionTexture;
    private MaterialPropertyBlock surfaceProperties;
    private float nextRenderTime;
    private float reflectionStrength;
    private bool rendering;

    public void Configure(Camera camera, Renderer surface, Material material)
    {
        playerCamera = camera;
        mirrorSurface = surface;
        mirrorMaterial = material;
    }

    private void Awake()
    {
        if (playerCamera == null || mirrorSurface == null || mirrorMaterial == null)
        {
            enabled = false;
            return;
        }

        reflectionTexture = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGBHalf)
        {
            name = "Signal Archive Floor Reflection",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1,
            useMipMap = false,
        };
        reflectionTexture.Create();
        GameObject cameraObject = new("Signal Archive Reflection Camera")
        {
            hideFlags = HideFlags.HideAndDontSave,
            tag = "Untagged",
        };
        cameraObject.transform.SetParent(transform, false);
        reflectionCamera = cameraObject.AddComponent<Camera>();
        reflectionCamera.enabled = false;
        UniversalAdditionalCameraData data = reflectionCamera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.volumeLayerMask = 0;
        reflectionStrength = mirrorMaterial.HasProperty(ReflectionStrengthId)
            ? mirrorMaterial.GetFloat(ReflectionStrengthId) : 0.65f;
        surfaceProperties = new MaterialPropertyBlock();
        UpdateSurface(false);
    }

    private void LateUpdate()
    {
        if (rendering || reflectionCamera == null || playerCamera == null || mirrorSurface == null
            || RuntimeSceneSwitcher.IsOpen || Time.time < nextRenderTime) return;

        Vector3 eye = playerCamera.transform.position;
        float planeY = mirrorSurface.bounds.max.y;
        if (!mirrorSurface.enabled || !playerCamera.isActiveAndEnabled || eye.y > -0.5f
            || eye.y <= planeY + 0.05f || mirrorSurface.bounds.SqrDistance(eye) > 15f * 15f) return;

        nextRenderTime = Time.time + 1f / 12f;
        RenderReflection(planeY);
    }

    private void RenderReflection(float planeY)
    {
        // Reflection in y = planeY: x and z are unchanged, y becomes 2h - y.
        Matrix4x4 reflection = Matrix4x4.identity;
        reflection.m11 = -1f;
        reflection.m13 = 2f * planeY;
        reflectionCamera.CopyFrom(playerCamera);
        reflectionCamera.enabled = false;
        reflectionCamera.targetTexture = reflectionTexture;
        reflectionCamera.rect = new Rect(0f, 0f, 1f, 1f);
        reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
        reflectionCamera.backgroundColor = new Color(0.003f, 0.006f, 0.01f, 1f);
        reflectionCamera.allowHDR = true;
        reflectionCamera.allowMSAA = false;
        reflectionCamera.useOcclusionCulling = false;
        reflectionCamera.transform.SetPositionAndRotation(
            reflection.MultiplyPoint(playerCamera.transform.position),
            Quaternion.LookRotation(reflection.MultiplyVector(playerCamera.transform.forward),
                reflection.MultiplyVector(playerCamera.transform.up)));
        reflectionCamera.worldToCameraMatrix = playerCamera.worldToCameraMatrix * reflection;

        // Preserve the real camera's aspect/projection despite the square target.
        // Only its near clipping plane changes, to exclude the floor's underside.
        reflectionCamera.projectionMatrix = playerCamera.projectionMatrix;
        Matrix4x4 view = reflectionCamera.worldToCameraMatrix;
        Vector3 clipPoint = view.MultiplyPoint(new Vector3(0f, planeY + 0.025f, 0f));
        Vector3 clipNormal = view.MultiplyVector(Vector3.up).normalized;
        Vector4 clipPlane = new(clipNormal.x, clipNormal.y, clipNormal.z,
            -Vector3.Dot(clipPoint, clipNormal));
        reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(clipPlane);

        bool previousCulling = GL.invertCulling;
        bool surfaceWasEnabled = mirrorSurface.enabled;
        RenderTexture previousTarget = RenderTexture.active;
        rendering = true;
        try
        {
            mirrorSurface.enabled = false;
            GL.invertCulling = !previousCulling;
            reflectionCamera.Render();
            UpdateSurface(true);
        }
        catch (System.Exception exception)
        {
            // Fall back to the ordinary dark floor if this device cannot render
            // a secondary camera. Report once instead of retrying every frame.
            UpdateSurface(false);
            Debug.LogWarning($"Archive floor reflection disabled: {exception.Message}", this);
            enabled = false;
        }
        finally
        {
            GL.invertCulling = previousCulling;
            RenderTexture.active = previousTarget;
            if (mirrorSurface != null) mirrorSurface.enabled = surfaceWasEnabled;
            rendering = false;
        }
    }

    private void UpdateSurface(bool hasReflection)
    {
        if (mirrorSurface == null || surfaceProperties == null) return;
        mirrorSurface.GetPropertyBlock(surfaceProperties);
        surfaceProperties.SetTexture(ReflectionTextureId,
            hasReflection ? reflectionTexture : Texture2D.blackTexture);
        surfaceProperties.SetFloat(ReflectionStrengthId, hasReflection ? reflectionStrength : 0f);
        mirrorSurface.SetPropertyBlock(surfaceProperties);
    }

    private void OnDisable() => UpdateSurface(false);

    private void OnDestroy()
    {
        UpdateSurface(false);
        if (reflectionCamera != null) Destroy(reflectionCamera.gameObject);
        if (reflectionTexture != null)
        {
            reflectionTexture.Release();
            Destroy(reflectionTexture);
        }
    }
}
