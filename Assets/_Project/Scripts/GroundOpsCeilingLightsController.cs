using UnityEngine;

[ExecuteAlways]
public sealed class GroundOpsCeilingLightsController : MonoBehaviour
{
    [SerializeField] private Light[] lights;
    [SerializeField] private Renderer[] luminousRenderers;
    [SerializeField] private Material lightsOnMaterial;
    [SerializeField] private Material lightsOffMaterial;
    [SerializeField] private bool lightsOn = true;

    public bool LightsOn => lightsOn;

    public void Configure(
        Light[] controlledLights,
        Renderer[] luminousSurfaces,
        Material onMaterial,
        Material offMaterial,
        bool initialState)
    {
        lights = controlledLights;
        luminousRenderers = luminousSurfaces;
        lightsOnMaterial = onMaterial;
        lightsOffMaterial = offMaterial;
        lightsOn = initialState;
        ApplyState();
    }

    public void SetLightsOn(bool enabledState)
    {
        lightsOn = enabledState;
        ApplyState();
    }

    private void OnEnable()
    {
        ApplyState();
    }

    private void OnValidate()
    {
        ApplyState();
    }

    private void ApplyState()
    {
        if (lights != null)
        {
            foreach (Light controlledLight in lights)
            {
                if (controlledLight != null) controlledLight.enabled = lightsOn;
            }
        }

        if (luminousRenderers == null) return;
        Material targetMaterial = lightsOn ? lightsOnMaterial : lightsOffMaterial;
        foreach (Renderer luminousRenderer in luminousRenderers)
        {
            if (luminousRenderer != null && targetMaterial != null)
            {
                luminousRenderer.sharedMaterial = targetMaterial;
            }
        }
    }
}
