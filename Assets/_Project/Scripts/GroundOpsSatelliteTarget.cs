using UnityEngine;

[ExecuteAlways]
public sealed class GroundOpsSatelliteTarget : MonoBehaviour
{
    [SerializeField] private string targetName = "GOES-19 (GOES East)";
    [SerializeField] private float azimuthDegrees = 166.823f;
    [SerializeField] private float elevationDegrees = 44.946f;
    [SerializeField] private float rangeKilometers = 37409.234f;
    [SerializeField] private float frequencyMegahertz = 8220f;
    [SerializeField] private float powerDbmiEirp = 69.6f;

    public string TargetName => targetName;
    public float AzimuthDegrees => azimuthDegrees;
    public float ElevationDegrees => elevationDegrees;
    public float RangeKilometers => rangeKilometers;
    public float FrequencyMegahertz => frequencyMegahertz;
    public float PowerDbmiEirp => powerDbmiEirp;

    public void Configure(
        string name,
        float azimuth,
        float elevation,
        float range,
        float frequency,
        float power)
    {
        targetName = name;
        azimuthDegrees = azimuth;
        elevationDegrees = elevation;
        rangeKilometers = Mathf.Max(0f, range);
        frequencyMegahertz = Mathf.Max(0f, frequency);
        powerDbmiEirp = power;
    }
}
