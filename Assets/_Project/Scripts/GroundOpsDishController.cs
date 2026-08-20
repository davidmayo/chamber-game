using UnityEngine;

[ExecuteAlways]
public sealed class GroundOpsDishController : MonoBehaviour
{
    [SerializeField] private Transform[] reflectors;
    [SerializeField] private Vector3 worldNorth = Vector3.forward;
    [SerializeField] private Vector3 worldEast = Vector3.right;
    [SerializeField] private float azimuthDegrees = 98f;
    [SerializeField] private float elevationDegrees = 20f;
    [SerializeField, Min(0f)] private float azimuthSpeedDegreesPerSecond = 12f;
    [SerializeField, Min(0f)] private float elevationSpeedDegreesPerSecond = 12f;
    [SerializeField] private Vector2 azimuthLimitsDegrees = new(-180f, 180f);
    [SerializeField] private Vector2 elevationLimitsDegrees = new(0f, 90f);

    public float AzimuthDegrees => azimuthDegrees;
    public float ElevationDegrees => elevationDegrees;

    public void Configure(
        Transform[] dishReflectors,
        Vector3 north,
        Vector3 east,
        float initialAzimuth,
        float initialElevation)
    {
        reflectors = dishReflectors;
        worldNorth = Vector3.ProjectOnPlane(north, Vector3.up).normalized;
        worldEast = Vector3.ProjectOnPlane(east, Vector3.up).normalized;
        azimuthSpeedDegreesPerSecond = 12f;
        elevationSpeedDegreesPerSecond = 12f;
        azimuthDegrees = Mathf.Clamp(
            initialAzimuth,
            azimuthLimitsDegrees.x,
            azimuthLimitsDegrees.y);
        elevationDegrees = Mathf.Clamp(
            initialElevation,
            elevationLimitsDegrees.x,
            elevationLimitsDegrees.y);
        ApplyPose();
    }

    public void ApplyInput(float azimuthInput, float elevationInput, float deltaTime)
    {
        azimuthDegrees = Mathf.Clamp(
            azimuthDegrees
            + Mathf.Clamp(azimuthInput, -5f, 5f)
            * azimuthSpeedDegreesPerSecond
            * deltaTime,
            azimuthLimitsDegrees.x,
            azimuthLimitsDegrees.y);
        elevationDegrees = Mathf.Clamp(
            elevationDegrees
            + Mathf.Clamp(elevationInput, -5f, 5f)
            * elevationSpeedDegreesPerSecond
            * deltaTime,
            elevationLimitsDegrees.x,
            elevationLimitsDegrees.y);
        ApplyPose();
    }

    public void SetPose(float azimuth, float elevation)
    {
        azimuthDegrees = Mathf.Clamp(
            azimuth,
            azimuthLimitsDegrees.x,
            azimuthLimitsDegrees.y);
        elevationDegrees = Mathf.Clamp(
            elevation,
            elevationLimitsDegrees.x,
            elevationLimitsDegrees.y);
        ApplyPose();
    }

    private void OnValidate()
    {
        SetPose(azimuthDegrees, elevationDegrees);
    }

    private void ApplyPose()
    {
        if (reflectors == null || worldNorth.sqrMagnitude < 0.9f || worldEast.sqrMagnitude < 0.9f)
        {
            return;
        }

        float azimuthRadians = azimuthDegrees * Mathf.Deg2Rad;
        float elevationRadians = elevationDegrees * Mathf.Deg2Rad;
        Vector3 horizontalDirection =
            worldNorth * Mathf.Cos(azimuthRadians)
            + worldEast * Mathf.Sin(azimuthRadians);
        Vector3 pointingDirection =
            horizontalDirection * Mathf.Cos(elevationRadians)
            + Vector3.up * Mathf.Sin(elevationRadians);

        foreach (Transform reflector in reflectors)
        {
            if (reflector != null)
            {
                // The proxy reflector is a thin cylinder, whose local Y axis is
                // its boresight. Azimuth is a true compass bearing: 0 = north,
                // +90 = east. Positive elevation points above the horizon.
                reflector.rotation = Quaternion.FromToRotation(
                    Vector3.up,
                    pointingDirection.normalized);
            }
        }
    }

}
