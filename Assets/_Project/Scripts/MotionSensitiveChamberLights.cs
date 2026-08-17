using UnityEngine;

public sealed class MotionSensitiveChamberLights : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Light[] lights;
    [SerializeField] private Renderer[] illuminatedPanels;
    [SerializeField] private Material onMaterial;
    [SerializeField] private Material offMaterial;
    [SerializeField, Min(0f)] private float shutoffDelaySeconds = 30f;

    private Vector3 previousPlayerPosition;
    private Quaternion previousPlayerRotation;
    private float remainingSeconds;
    private bool lightsOn;

    public void Configure(
        Transform playerTransform,
        Light[] controlledLights,
        Renderer[] panels,
        Material illuminatedMaterial,
        Material darkMaterial)
    {
        player = playerTransform;
        lights = controlledLights;
        illuminatedPanels = panels;
        onMaterial = illuminatedMaterial;
        offMaterial = darkMaterial;
        remainingSeconds = shutoffDelaySeconds;
        SetLights(true);
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
        SetLights(true);
    }

    private void Update()
    {
        Vector3 currentPosition = player.position;
        Quaternion currentRotation = player.rotation;
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
        if (lightsOn == enabledState && Application.isPlaying)
        {
            return;
        }

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
