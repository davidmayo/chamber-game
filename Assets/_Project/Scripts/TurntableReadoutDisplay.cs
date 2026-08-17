using UnityEngine;
using UnityEngine.UI;

public sealed class TurntableReadoutDisplay : MonoBehaviour
{
    [SerializeField] private TurntableController turntableController;
    [SerializeField] private Text readout;

    public void Configure(TurntableController controller, Text text)
    {
        turntableController = controller;
        readout = text;
        UpdateReadout();
    }

    private void Update()
    {
        UpdateReadout();
    }

    private void UpdateReadout()
    {
        if (turntableController == null || readout == null)
        {
            return;
        }

        readout.text =
            $"Pan: {FormatAngle(turntableController.PanDegrees)} / "
            + $"Tilt: {FormatAngle(turntableController.TiltDegrees)}";
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
