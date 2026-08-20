using UnityEngine;
using UnityEngine.UI;

public sealed class GroundOpsDishReadoutDisplay : MonoBehaviour
{
    [SerializeField] private GroundOpsDishController dishController;
    [SerializeField] private Text readout;

    public void Configure(GroundOpsDishController controller, Text text)
    {
        dishController = controller;
        readout = text;
        UpdateReadout();
    }

    private void Update()
    {
        UpdateReadout();
    }

    private void UpdateReadout()
    {
        if (dishController == null || readout == null)
        {
            return;
        }

        readout.text =
            $"Azimuth: {FormatAngle(dishController.AzimuthDegrees)}\n"
            + $"Elevation: {FormatAngle(dishController.ElevationDegrees)}";
    }

    private static string FormatAngle(float degrees)
    {
        return degrees > 0.0049f
            ? $"+{degrees:0.00}\u00B0"
            : degrees < -0.0049f
                ? $"{degrees:0.00}\u00B0"
                : "0.00\u00B0";
    }
}
