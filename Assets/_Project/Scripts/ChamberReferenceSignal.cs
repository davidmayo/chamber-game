using UnityEngine;
using UnityEngine.UI;

// A deliberately simple reference measurement for the playable commissioning
// exercise. This is a tuning puzzle, not an RF prediction for the real chamber.
public sealed class ChamberReferenceSignal : MonoBehaviour
{
    public const float ReferencePan = 30f;
    public const float ReferenceTilt = -10f;
    public const float ReferencePolarity = 90f;

    [SerializeField] private TurntableController table;
    [SerializeField] private SourceAntennaController source;
    [SerializeField] private Text readout;

    public float PowerDbm { get; private set; } = -110f;
    public float Quality01 => Mathf.InverseLerp(-110f, -42f, PowerDbm);
    public bool IsAligned => table != null && source != null
        && Mathf.Abs(table.PanDegrees - ReferencePan) <= 2f
        && Mathf.Abs(table.TiltDegrees - ReferenceTilt) <= 2f
        && Mathf.Abs(Mathf.DeltaAngle(source.PolarityDegrees, ReferencePolarity)) <= 3f;

    public void Configure(TurntableController positioner, SourceAntennaController antenna, Text display)
    {
        table = positioner;
        source = antenna;
        readout = display;
    }

    private void Update()
    {
        if (table == null || source == null) return;
        float pan = (table.PanDegrees - ReferencePan) / 22f;
        float tilt = (table.TiltDegrees - ReferenceTilt) / 18f;
        float polarity = Mathf.DeltaAngle(source.PolarityDegrees, ReferencePolarity) / 45f;
        PowerDbm = Mathf.Max(-110f, -42f - 24f * (pan * pan + tilt * tilt + polarity * polarity));
        if (readout != null)
        {
            readout.text = $"REFERENCE RECEIVER / {PowerDbm:0.0} dBm\n"
                + $"Pan {table.PanDegrees:0.0} / Tilt {table.TiltDegrees:0.0}\n"
                + $"Polarity {source.PolarityDegrees:0.0}\n\n"
                + (IsAligned ? "REFERENCE MATCH / HOLD SPACE" : "TARGET +30 / -10 / 90\nSHIFT FOR FINE ADJUSTMENT");
        }
    }
}
