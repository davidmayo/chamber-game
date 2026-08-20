using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class GroundOpsSignalDisplay : MonoBehaviour
{
    [SerializeField] private GroundOpsDishController dishController;
    [SerializeField] private GroundOpsSatelliteTarget satelliteTarget;
    [SerializeField] private Text readout;
    [SerializeField] private float perfectAlignmentPowerDbm = -85.2f;
    [SerializeField, Min(0.01f)] private float halfPowerBeamWidthDegrees = 15f;
    [SerializeField] private float minimumDisplayedPowerDbm = -160f;

    public float OffAxisDegrees { get; private set; }
    public float ReceivedPowerDbm { get; private set; }

    public void Configure(
        GroundOpsDishController dishes,
        GroundOpsSatelliteTarget target,
        Text text,
        float alignedPowerDbm,
        float beamWidthDegrees)
    {
        dishController = dishes;
        satelliteTarget = target;
        readout = text;
        perfectAlignmentPowerDbm = alignedPowerDbm;
        halfPowerBeamWidthDegrees = Mathf.Max(0.01f, beamWidthDegrees);
        UpdateReadout();
    }

    private void Update()
    {
        UpdateReadout();
    }

    private void UpdateReadout()
    {
        if (dishController == null || satelliteTarget == null || readout == null)
        {
            return;
        }

        Vector3 dishDirection = DirectionFromAzimuthElevation(
            dishController.AzimuthDegrees,
            dishController.ElevationDegrees);
        Vector3 targetDirection = DirectionFromAzimuthElevation(
            satelliteTarget.AzimuthDegrees,
            satelliteTarget.ElevationDegrees);
        OffAxisDegrees = Vector3.Angle(dishDirection, targetDirection);

        // Intentionally crude temporary antenna model. With this coefficient,
        // the response is 3 dB down at half of the full HPBW.
        float normalizedOffset = OffAxisDegrees / halfPowerBeamWidthDegrees;
        float attenuationDb = 12f * normalizedOffset * normalizedOffset;
        ReceivedPowerDbm = Mathf.Max(
            minimumDisplayedPowerDbm,
            perfectAlignmentPowerDbm - attenuationDb);

        readout.text =
            $"Power: {ReceivedPowerDbm:0.0} dBm\n"
            + $"Frequency: {satelliteTarget.FrequencyMegahertz:0.000} MHz\n"
            + $"ID: {satelliteTarget.TargetName}";
    }

    private static Vector3 DirectionFromAzimuthElevation(float azimuth, float elevation)
    {
        float azimuthRadians = azimuth * Mathf.Deg2Rad;
        float elevationRadians = elevation * Mathf.Deg2Rad;
        float horizontal = Mathf.Cos(elevationRadians);
        return new Vector3(
            horizontal * Mathf.Sin(azimuthRadians),
            Mathf.Sin(elevationRadians),
            horizontal * Mathf.Cos(azimuthRadians));
    }
}
