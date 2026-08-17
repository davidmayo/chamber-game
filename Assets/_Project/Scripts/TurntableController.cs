using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TurntableController : MonoBehaviour
{
    [Header("Controlled assemblies")]
    [SerializeField] private Transform panAssembly;
    [SerializeField] private Transform tiltAssembly;
    [SerializeField] private Transform heightAssembly;
    [SerializeField] private Transform[] risingForwardForks;
    [SerializeField] private Transform[] risingBackwardForks;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float panSpeedDegreesPerSecond = 60f;
    [SerializeField, Min(0f)] private float tiltSpeedDegreesPerSecond = 60f;
    [SerializeField, Min(0f)] private float heightSpeedMetersPerSecond = 0.25f;
    [SerializeField] private Vector2 panLimitsDegrees = new(-180f, 180f);
    [SerializeField] private Vector2 tiltLimitsDegrees = new(-90f, 45f);
    [SerializeField] private Vector2 heightLimitsMeters = new(0f, 1f);

    [Header("Current pose")]
    [SerializeField] private float panDegrees;
    [SerializeField] private float tiltDegrees;
    [SerializeField] private float heightMeters = 0.2f;

    [Header("HUD")]
    [SerializeField] private bool showHud = true;
    [SerializeField] private Vector2 hudPosition = new(16f, 16f);

    public float PanDegrees => panDegrees;
    public float TiltDegrees => tiltDegrees;
    public float HeightMeters => heightMeters;
    public Vector2 PanLimitsDegrees => panLimitsDegrees;
    public Vector2 TiltLimitsDegrees => tiltLimitsDegrees;
    public Vector2 HeightLimitsMeters => heightLimitsMeters;

    private GUIStyle hudTitleStyle;
    private GUIStyle hudValueStyle;
    private GUIStyle hudHintStyle;

    public void Configure(
        Transform pan,
        Transform tilt,
        Transform height,
        Transform[] forwardForks,
        Transform[] backwardForks)
    {
        panAssembly = pan;
        tiltAssembly = tilt;
        heightAssembly = height;
        risingForwardForks = forwardForks;
        risingBackwardForks = backwardForks;
        ApplyPose();
    }

    private void Awake()
    {
        if (panAssembly == null || tiltAssembly == null || heightAssembly == null)
        {
            Debug.LogError("TurntableController requires pan, tilt, and height assembly references.", this);
            enabled = false;
            return;
        }

        ClampPose();
        ApplyPose();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // Reversed by design: A pans right and D pans left.
        float panInput = ButtonAxis(keyboard.dKey.isPressed, keyboard.aKey.isPressed);
        float tiltInput = ButtonAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
        float heightInput = ButtonAxis(keyboard.qKey.isPressed, keyboard.eKey.isPressed);
        ApplyInput(panInput, tiltInput, heightInput, Time.deltaTime);
    }

    public void ApplyInput(float panInput, float tiltInput, float deltaTime)
    {
        ApplyInput(panInput, tiltInput, 0f, deltaTime);
    }

    public void ApplyInput(float panInput, float tiltInput, float heightInput, float deltaTime)
    {
        panDegrees += Mathf.Clamp(panInput, -1f, 1f) * panSpeedDegreesPerSecond * deltaTime;
        tiltDegrees += Mathf.Clamp(tiltInput, -1f, 1f) * tiltSpeedDegreesPerSecond * deltaTime;
        heightMeters += Mathf.Clamp(heightInput, -1f, 1f) * heightSpeedMetersPerSecond * deltaTime;
        ClampPose();
        ApplyPose();
    }

    public void SetPose(float pan, float tilt, float height)
    {
        panDegrees = pan;
        tiltDegrees = tilt;
        heightMeters = height;
        ClampPose();
        ApplyPose();
    }

    public void SetPanDegrees(float degrees)
    {
        SetPose(degrees, tiltDegrees, heightMeters);
    }

    public void SetTiltDegrees(float degrees)
    {
        SetPose(panDegrees, degrees, heightMeters);
    }

    public void SetHeightMeters(float meters)
    {
        SetPose(panDegrees, tiltDegrees, meters);
    }

    [ContextMenu("Reset Pose")]
    public void ResetPose()
    {
        panDegrees = 0f;
        tiltDegrees = 0f;
        heightMeters = 0.2f;
        ApplyPose();
    }

    private void ClampPose()
    {
        panDegrees = Mathf.Clamp(panDegrees, panLimitsDegrees.x, panLimitsDegrees.y);
        tiltDegrees = Mathf.Clamp(tiltDegrees, tiltLimitsDegrees.x, tiltLimitsDegrees.y);
        heightMeters = Mathf.Clamp(heightMeters, heightLimitsMeters.x, heightLimitsMeters.y);
    }

    private void ApplyPose()
    {
        if (panAssembly != null)
        {
            // Positive player input pans toward screen-right from the antenna's zero pose.
            panAssembly.localRotation = Quaternion.Euler(0f, -panDegrees, 0f);
        }

        if (tiltAssembly != null)
        {
            // The antenna points along local -Z, so positive X rotation tilts it upward.
            tiltAssembly.localRotation = Quaternion.Euler(tiltDegrees, 0f, 0f);
        }

        if (heightAssembly != null)
        {
            Vector3 position = heightAssembly.localPosition;
            position.y = heightMeters;
            heightAssembly.localPosition = position;
            UpdateScissorForks();
        }
    }

    private void UpdateScissorForks()
    {
        const float nominalForkLength = 1.2f;
        float horizontalSpan = Mathf.Sqrt(Mathf.Max(
            0.05f * 0.05f,
            nominalForkLength * nominalForkLength - heightMeters * heightMeters));
        float halfSpan = horizontalSpan / 2f;

        UpdateForkSet(risingForwardForks, -halfSpan, halfSpan);
        UpdateForkSet(risingBackwardForks, halfSpan, -halfSpan);
    }

    private void UpdateForkSet(Transform[] forks, float startZOffset, float endZOffset)
    {
        if (forks == null)
        {
            return;
        }

        foreach (Transform fork in forks)
        {
            if (fork == null)
            {
                continue;
            }

            float x = fork.localPosition.x;
            Vector3 start = new(x, 0.6f, 3.9f + startZOffset);
            Vector3 end = new(x, 0.6f + heightMeters, 3.9f + endZOffset);
            Vector3 direction = end - start;
            fork.localPosition = (start + end) / 2f;
            fork.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            fork.localScale = new Vector3(0.05f, direction.magnitude, 0.05f);
        }
    }

    private void OnGUI()
    {
        if (!showHud)
        {
            return;
        }

        EnsureHudStyles();
        Rect panel = new(hudPosition.x, hudPosition.y, 250f, 156f);
        Color previousColor = GUI.color;
        GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, panel.height - 20f));
        GUILayout.Label("POSITIONER", hudTitleStyle);
        GUILayout.Space(4f);
        GUILayout.Label($"PAN       {panDegrees,8:0.0}°", hudValueStyle);
        GUILayout.Label($"TILT      {tiltDegrees,8:0.0}°", hudValueStyle);
        GUILayout.Label($"HEIGHT    {heightMeters,8:0.000} m", hudValueStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label("A/D PAN   W/S TILT   Q/E HEIGHT", hudHintStyle);
        GUILayout.EndArea();

        GUI.color = previousColor;
    }

    private void EnsureHudStyles()
    {
        if (hudTitleStyle != null)
        {
            return;
        }

        hudTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.45f, 0.82f, 1f) },
        };
        hudValueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
        hudHintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = new Color(0.72f, 0.76f, 0.82f) },
        };
    }

    private static float ButtonAxis(bool negativePressed, bool positivePressed)
    {
        return (positivePressed ? 1f : 0f) - (negativePressed ? 1f : 0f);
    }
}
