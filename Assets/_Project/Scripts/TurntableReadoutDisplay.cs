using UnityEngine;
using UnityEngine.UI;

public sealed class TurntableReadoutDisplay : MonoBehaviour
{
    [SerializeField] private TurntableController turntableController;
    [SerializeField] private SourceAntennaController sourceAntennaController;
    [SerializeField] private Text readout;

    public void Configure(
        TurntableController controller,
        SourceAntennaController sourceAntenna,
        Text text)
    {
        turntableController = controller;
        sourceAntennaController = sourceAntenna;
        readout = text;
        UpdateReadout();
    }

    private void Update()
    {
        UpdateReadout();
    }

    private void UpdateReadout()
    {
        if (turntableController == null || sourceAntennaController == null || readout == null)
        {
            return;
        }

        readout.text =
            $"Pan: {FormatAngle(turntableController.PanDegrees)}\n"
            + $"Tilt: {FormatAngle(turntableController.TiltDegrees)}\n"
            + $"Polarity: {sourceAntennaController.PolarityDegrees:0}\u00B0";
    }

    private static string FormatAngle(float degrees)
    {
        return degrees > 0.049f
            ? $"+{degrees:0}°"
            : degrees < -0.049f
                ? $"{degrees:0}°"
                : "0°";
    }
}
