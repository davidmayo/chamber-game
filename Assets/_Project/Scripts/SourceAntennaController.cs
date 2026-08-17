using UnityEngine;

public sealed class SourceAntennaController : MonoBehaviour
{
    [SerializeField] private Transform polarityAssembly;
    [SerializeField, Min(0f)] private float speedDegreesPerSecond = 60f;
    [SerializeField] private float polarityDegrees;

    public float PolarityDegrees => polarityDegrees;

    public void Configure(Transform assembly, float initialPolarityDegrees = 0f)
    {
        polarityAssembly = assembly;
        polarityDegrees = NormalizeAngle(initialPolarityDegrees);
        ApplyPose();
    }

    public void ApplyInput(float input, float deltaTime)
    {
        polarityDegrees = NormalizeAngle(
            polarityDegrees + Mathf.Clamp(input, -1f, 1f) * speedDegreesPerSecond * deltaTime);
        ApplyPose();
    }

    public void SetPolarityDegrees(float degrees)
    {
        polarityDegrees = NormalizeAngle(degrees);
        ApplyPose();
    }

    private void Awake()
    {
        if (polarityAssembly == null)
        {
            Debug.LogError("SourceAntennaController requires a polarity assembly.", this);
            enabled = false;
            return;
        }

        ApplyPose();
    }

    private void ApplyPose()
    {
        if (polarityAssembly != null)
        {
            polarityAssembly.localRotation = Quaternion.Euler(0f, 0f, polarityDegrees);
        }
    }

    private static float NormalizeAngle(float degrees)
    {
        return Mathf.Repeat(degrees + 180f, 360f) - 180f;
    }
}
