using UnityEngine;

public enum ChamberLightMode
{
    Auto,
    On,
    Off,
}

public sealed class MotionSensitiveChamberLights : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Light[] lights;
    [SerializeField] private Renderer[] illuminatedPanels;
    [SerializeField] private Material onMaterial;
    [SerializeField] private Material offMaterial;
    [SerializeField] private ChamberLightMode mode = ChamberLightMode.Auto;
    [SerializeField, Min(0f)] private float shutoffDelaySeconds = 30f;
    [SerializeField] private bool lightsOn = true;

    private Vector3 previousPlayerPosition;
    private Quaternion previousPlayerRotation;
    private float remainingSeconds;

    public ChamberLightMode Mode => mode;
    public float TimeoutSeconds => shutoffDelaySeconds;
    public float RemainingSeconds => Application.isPlaying
        ? Mathf.Max(0f, remainingSeconds)
        : shutoffDelaySeconds;
    public bool LightsOn => lightsOn;

    public void Configure(
        Transform playerTransform,
        Light[] controlledLights,
        Renderer[] panels,
        Material illuminatedMaterial,
        Material darkMaterial,
        ChamberLightMode initialMode,
        float initialTimeoutSeconds)
    {
        player = playerTransform;
        lights = controlledLights;
        illuminatedPanels = panels;
        onMaterial = illuminatedMaterial;
        offMaterial = darkMaterial;
        mode = initialMode;
        shutoffDelaySeconds = Mathf.Clamp(initialTimeoutSeconds, 1f, 120f);
        remainingSeconds = shutoffDelaySeconds;
        ApplyMode();
    }

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogError("MotionSensitiveChamberLights requires a player reference.", this);
            enabled = false;
            return;
        }

        previousPlayerPosition = player.position;
        previousPlayerRotation = player.rotation;
        remainingSeconds = shutoffDelaySeconds;
        ApplyMode();
    }

    private void Update()
    {
        Vector3 currentPosition = player.position;
        Quaternion currentRotation = player.rotation;
        if (mode != ChamberLightMode.Auto)
        {
            previousPlayerPosition = currentPosition;
            previousPlayerRotation = currentRotation;
            return;
        }

        bool playerMoved = (currentPosition - previousPlayerPosition).sqrMagnitude > 0.000001f
            || Quaternion.Angle(currentRotation, previousPlayerRotation) > 0.05f;

        if (playerMoved && IsInsideChamber(currentPosition))
        {
            remainingSeconds = shutoffDelaySeconds;
            SetLights(true);
        }
        else
        {
            remainingSeconds -= Time.deltaTime;
            if (remainingSeconds <= 0f)
            {
                SetLights(false);
            }
        }

        previousPlayerPosition = currentPosition;
        previousPlayerRotation = currentRotation;
    }

    public void SetMode(ChamberLightMode newMode)
    {
        mode = newMode;
        remainingSeconds = shutoffDelaySeconds;
        ApplyMode();
    }

    public void SetTimeoutSeconds(float seconds)
    {
        shutoffDelaySeconds = Mathf.Clamp(seconds, 1f, 120f);
        remainingSeconds = shutoffDelaySeconds;
        if (mode == ChamberLightMode.Auto)
        {
            SetLights(true);
        }
    }

    private void ApplyMode()
    {
        switch (mode)
        {
            case ChamberLightMode.On:
                SetLights(true);
                break;
            case ChamberLightMode.Off:
                SetLights(false);
                break;
            default:
                SetLights(true);
                break;
        }
    }

    private static bool IsInsideChamber(Vector3 position)
    {
        if (position.z < -5f || position.z > 5f)
        {
            return false;
        }

        float halfWidth = position.z >= 0f
            ? 2.5f
            : Mathf.Lerp(2.5f, 0.5f, -position.z / 5f);
        return Mathf.Abs(position.x) <= halfWidth;
    }

    private void SetLights(bool enabledState)
    {
        lightsOn = enabledState;
        if (lights != null)
        {
            foreach (Light controlledLight in lights)
            {
                if (controlledLight != null)
                {
                    controlledLight.enabled = enabledState;
                }
            }
        }

        if (illuminatedPanels != null)
        {
            foreach (Renderer panel in illuminatedPanels)
            {
                if (panel != null)
                {
                    panel.sharedMaterial = enabledState ? onMaterial : offMaterial;
                }
            }
        }
    }
}
