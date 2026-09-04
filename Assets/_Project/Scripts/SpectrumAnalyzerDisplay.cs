using UnityEngine;

public sealed class SpectrumAnalyzerDisplay : MonoBehaviour
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private Texture defaultTexture;
    [SerializeField] private ChamberReferenceSignal reference;

    private MaterialPropertyBlock propertyBlock;
    private Texture2D liveTrace;
    private Color32[] pixels;
    private float nextRefresh;
    private const int Width = 512;
    private const int Height = 256;

    public void ConfigureSignal(ChamberReferenceSignal signal) => reference = signal;

    public void Configure(Renderer renderer, Texture texture)
    {
        screenRenderer = renderer;
        defaultTexture = texture;
        SetTexture(defaultTexture);
    }

    public void SetTexture(Texture texture)
    {
        if (screenRenderer == null || texture == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        screenRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetTexture(BaseMapId, texture);
        screenRenderer.SetPropertyBlock(propertyBlock);
    }

    private void Awake()
    {
        SetTexture(defaultTexture);
    }

    private void Start()
    {
        if (reference == null) return;
        liveTrace = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        {
            name = "Live chamber reference trace",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        pixels = new Color32[Width * Height];
        SetTexture(liveTrace);
    }

    private void LateUpdate()
    {
        if (liveTrace == null || RuntimeSceneSwitcher.IsOpen || Time.time < nextRefresh) return;
        nextRefresh = Time.time + 0.1f;
        Color32 dark = new(2, 12, 12, 255);
        Color32 grid = new(12, 54, 43, 255);
        Color32 trace = new(100, 255, 171, 255);
        System.Array.Fill(pixels, dark);
        for (int x = 24; x <= 456; x += 48) Line(x, 24, x, 232, grid);
        for (int y = 24; y <= 232; y += 26) Line(24, y, 456, y, grid);
        float strength = reference.Quality01;
        int previousY = 32;
        for (int x = 24; x <= 456; x++)
        {
            float offset = (x - 252f) / 34f;
            float peak = Mathf.Exp(-offset * offset) * (20f + strength * 174f);
            float noise = Mathf.Sin(x * 2.73f + Time.time * 4f) * 3f + Mathf.Sin(x * 0.53f) * 2f;
            int y = Mathf.Clamp(Mathf.RoundToInt(34f + peak + noise), 25, 230);
            if (x > 24) Line(x - 1, previousY, x, y, trace);
            previousY = y;
        }
        // The side bar makes the peak strength legible even at a distance.
        Color32 meter = reference.IsAligned ? trace : new Color32(236, 194, 95, 255);
        for (int x = 478; x < 494; x++)
            Line(x, 24, x, 24 + Mathf.RoundToInt(strength * 208f), meter);
        liveTrace.SetPixels32(pixels);
        liveTrace.Apply(false);
    }

    private void Line(int x0, int y0, int x1, int y1, Color32 color)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        for (int step = 0; step <= steps; step++)
        {
            float t = steps == 0 ? 0f : step / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            pixels[y * Width + x] = color;
        }
    }

    private void OnDestroy()
    {
        if (liveTrace != null) Destroy(liveTrace);
    }
}
