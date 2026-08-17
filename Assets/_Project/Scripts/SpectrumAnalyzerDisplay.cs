using UnityEngine;
using UnityEngine.UI;

public sealed class SpectrumAnalyzerDisplay : MonoBehaviour
{
    [SerializeField] private Text readout;
    [SerializeField] private LineRenderer trace;
    [SerializeField] private Vector2 traceSize = new(0.22f, 0.075f);
    [SerializeField] private Vector3 traceCenter = new(-0.045f, -0.012f, 0.001f);

    public void Configure(
        Text displayReadout,
        LineRenderer displayTrace,
        Vector2 displayTraceSize,
        Vector3 displayTraceCenter)
    {
        readout = displayReadout;
        trace = displayTrace;
        traceSize = displayTraceSize;
        traceCenter = displayTraceCenter;
    }

    public void SetReadout(string content)
    {
        if (readout != null)
        {
            readout.text = content;
        }
    }

    public void SetTrace(float[] normalizedSamples)
    {
        if (trace == null || normalizedSamples == null || normalizedSamples.Length < 2)
        {
            return;
        }

        trace.positionCount = normalizedSamples.Length;
        for (int index = 0; index < normalizedSamples.Length; index++)
        {
            float x = Mathf.Lerp(-traceSize.x / 2f, traceSize.x / 2f,
                index / (normalizedSamples.Length - 1f));
            float y = Mathf.Clamp01(normalizedSamples[index]) * traceSize.y - traceSize.y / 2f;
            trace.SetPosition(index, traceCenter + new Vector3(x, y, 0f));
        }
    }
}
