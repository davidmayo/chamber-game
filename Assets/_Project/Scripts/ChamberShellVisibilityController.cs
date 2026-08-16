using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public sealed class ChamberShellVisibilityController : MonoBehaviour
{
    [SerializeField] private Renderer[] volumeRenderers;
    [SerializeField] private Renderer[] cutawayRenderers;
    [SerializeField] private bool cutawayView;
    [SerializeField] private bool showHud = true;

    public bool CutawayView => cutawayView;

    public void Configure(Renderer[] volumes, Renderer[] cutawaySurfaces)
    {
        volumeRenderers = volumes;
        cutawayRenderers = cutawaySurfaces;
        ApplyVisibility();
    }

    private void Awake()
    {
        ApplyVisibility();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.vKey.wasPressedThisFrame)
        {
            ToggleVisibility();
        }
    }

    public void ToggleVisibility()
    {
        SetCutawayView(!cutawayView);
    }

    public void SetCutawayView(bool enabled)
    {
        cutawayView = enabled;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (volumeRenderers != null)
        {
            ShadowCastingMode volumeMode = cutawayView
                ? ShadowCastingMode.ShadowsOnly
                : ShadowCastingMode.On;

            foreach (Renderer volumeRenderer in volumeRenderers)
            {
                if (volumeRenderer != null)
                {
                    volumeRenderer.shadowCastingMode = volumeMode;
                }
            }
        }

        if (cutawayRenderers == null)
        {
            return;
        }

        foreach (Renderer cutawayRenderer in cutawayRenderers)
        {
            if (cutawayRenderer != null)
            {
                cutawayRenderer.enabled = cutawayView;
                cutawayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }

    private void OnGUI()
    {
        if (!showHud)
        {
            return;
        }

        Rect panel = new(16f, 180f, 250f, 38f);
        Color previousColor = GUI.color;
        GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle style = new(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal =
            {
                textColor = cutawayView
                    ? new Color(1f, 0.82f, 0.45f)
                    : new Color(0.72f, 0.86f, 1f),
            },
        };
        GUI.Label(panel, cutawayView ? "V  SHELL: CUTAWAY" : "V  SHELL: OPAQUE", style);
        GUI.color = previousColor;
    }
}
