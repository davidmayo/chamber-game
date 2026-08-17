using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public struct ShellVisualBinding
{
    public Renderer renderer;
    public Material opaqueMaterial;
    public Material transparentMaterial;

    public ShellVisualBinding(Renderer target, Material opaque, Material transparent)
    {
        renderer = target;
        opaqueMaterial = opaque;
        transparentMaterial = transparent;
    }
}

[ExecuteAlways]
public sealed class ChamberShellVisibilityController : MonoBehaviour
{
    [Header("Containing room")]
    [SerializeField] private Renderer[] roomPhysicalRenderers;
    [SerializeField] private ShellVisualBinding[] roomVisuals;
    [SerializeField] private Renderer[] roomCutawayRenderers;
    [SerializeField, Range(0f, 100f)] private float roomOpacityPercent = -1f;

    [Header("Chamber")]
    [SerializeField] private Renderer[] chamberPhysicalRenderers;
    [SerializeField] private ShellVisualBinding[] chamberVisuals;
    [SerializeField] private Renderer[] chamberCutawayRenderers;
    [SerializeField, Range(0f, 100f)] private float chamberOpacityPercent = -1f;

    [SerializeField, HideInInspector] private bool cutawayView;
    public float RoomOpacityPercent => ResolveOpacity(roomOpacityPercent);
    public float ChamberOpacityPercent => ResolveOpacity(chamberOpacityPercent);

    private void Awake()
    {
#if !UNITY_EDITOR
        ApplyStandaloneBuildDefaults();
#endif
    }

    public void ApplyStandaloneBuildDefaults()
    {
        roomOpacityPercent = ChamberBuildDefaults.RoomOpacityPercent;
        chamberOpacityPercent = ChamberBuildDefaults.ChamberOpacityPercent;
        cutawayView = false;
        ApplyVisibility();
    }

    public void Configure(
        Renderer[] roomPhysical,
        ShellVisualBinding[] roomCameraVisuals,
        Renderer[] roomCutaway,
        Renderer[] chamberPhysical,
        ShellVisualBinding[] chamberCameraVisuals,
        Renderer[] chamberCutaway,
        float initialRoomOpacity,
        float initialChamberOpacity)
    {
        roomPhysicalRenderers = roomPhysical;
        roomVisuals = roomCameraVisuals;
        roomCutawayRenderers = roomCutaway;
        chamberPhysicalRenderers = chamberPhysical;
        chamberVisuals = chamberCameraVisuals;
        chamberCutawayRenderers = chamberCutaway;
        roomOpacityPercent = Mathf.Clamp(initialRoomOpacity, 0f, 100f);
        chamberOpacityPercent = Mathf.Clamp(initialChamberOpacity, 0f, 100f);
        ApplyVisibility();
    }

    private void OnEnable()
    {
        ApplyVisibility();
    }

    public void ToggleVisibility()
    {
        bool bothCutaway = RoomOpacityPercent <= 0.01f && ChamberOpacityPercent <= 0.01f;
        float opacity = bothCutaway ? 100f : 0f;
        roomOpacityPercent = opacity;
        chamberOpacityPercent = opacity;
        cutawayView = !bothCutaway;
        ApplyVisibility();
    }

    public void SetRoomOpacityPercent(float opacity)
    {
        roomOpacityPercent = Mathf.Clamp(opacity, 0f, 100f);
        UpdateLegacyCutawayState();
        ApplyRoomVisibility();
    }

    public void SetChamberOpacityPercent(float opacity)
    {
        chamberOpacityPercent = Mathf.Clamp(opacity, 0f, 100f);
        UpdateLegacyCutawayState();
        ApplyChamberVisibility();
    }

    private void ApplyVisibility()
    {
        ApplyRoomVisibility();
        ApplyChamberVisibility();
    }

    private void ApplyRoomVisibility()
    {
        ApplyGroup(roomPhysicalRenderers, roomVisuals, roomCutawayRenderers, RoomOpacityPercent);
    }

    private void ApplyChamberVisibility()
    {
        ApplyGroup(chamberPhysicalRenderers, chamberVisuals, chamberCutawayRenderers,
            ChamberOpacityPercent);
    }

    private static void ApplyGroup(
        Renderer[] physicalRenderers,
        ShellVisualBinding[] cameraVisuals,
        Renderer[] cutawayRenderers,
        float opacityPercent)
    {
        SetPhysicalRenderers(physicalRenderers);
        if (cutawayRenderers != null)
        {
            foreach (Renderer renderer in cutawayRenderers)
            {
                if (renderer == null) continue;
                // These inward-facing surfaces make the far side fully opaque at every
                // slider value. Back-face culling naturally hides the near side.
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        if (cameraVisuals == null)
        {
            return;
        }

        float opacity = opacityPercent / 100f;
        bool cutaway = opacityPercent <= 0.01f;
        bool opaque = opacity >= 0.999f;
        foreach (ShellVisualBinding binding in cameraVisuals)
        {
            Renderer renderer = binding.renderer;
            if (renderer == null) continue;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.enabled = !cutaway;
            if (cutaway) continue;

            renderer.sharedMaterial = opaque
                ? binding.opaqueMaterial
                : binding.transparentMaterial;
            if (opaque)
            {
                renderer.SetPropertyBlock(null);
            }
            else
            {
                ApplyOpacity(renderer, binding.transparentMaterial, opacity);
            }
        }
    }

    private static void SetPhysicalRenderers(Renderer[] renderers)
    {
        if (renderers == null) return;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    private static void ApplyOpacity(Renderer renderer, Material material, float opacity)
    {
        Color color = material != null && material.HasProperty("_BaseColor")
            ? material.GetColor("_BaseColor")
            : Color.white;
        color.a = opacity;
        MaterialPropertyBlock properties = new();
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
    }

    private float ResolveOpacity(float serializedOpacity)
    {
        return serializedOpacity < 0f ? (cutawayView ? 0f : 100f) : serializedOpacity;
    }

    private void UpdateLegacyCutawayState()
    {
        cutawayView = RoomOpacityPercent <= 0.01f && ChamberOpacityPercent <= 0.01f;
    }

}
