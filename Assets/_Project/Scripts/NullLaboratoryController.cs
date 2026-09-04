using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A small cancellation experiment: match the amplitude and oppose the phase of
// an incoming tone. This teaches the idea visually; it is not an RF simulation.
public sealed class NullLaboratoryController : MonoBehaviour
{
    private const float IncomingPhase = 55f;
    private const float IncomingAmplitude = 0.65f;
    public const float NullPhase = -125f;
    public const float NullAmplitude = IncomingAmplitude;
    [SerializeField] private FirstPersonPlayerController player;
    [SerializeField] private Transform operations;
    [SerializeField] private SimpleSeatedConsoleController console;
    [SerializeField] private Transform supplyPoint;
    [SerializeField] private Transform supplyHandle;
    [SerializeField] private Text supplyLabel;
    [SerializeField] private Text readout;
    [SerializeField] private Text cellStatus;
    [SerializeField] private Renderer scope;
    [SerializeField] private Transform phaseKnob;
    [SerializeField] private Transform amplitudeKnob;
    [SerializeField] private Transform rotor;
    [SerializeField] private Light[] taskLights;
    [SerializeField, Min(0.1f)] private float captureSeconds = 2.5f;
    private float captureElapsed;
    private float nextTraceTime;
    private Texture2D trace;
    private Color32[] pixels;
    private MaterialPropertyBlock scopeProperties;
    private AudioSource tone;
    private AudioClip toneClip;
    private FacilityPlayerEffects effects;
    private const int Width = 512;
    private const int Height = 256;

    public bool Powered { get; private set; }
    public bool Certified { get; private set; }
    public float PhaseDegrees { get; private set; }
    public float Amplitude { get; private set; } = 1f;
    public float CaptureProgress01 => Mathf.Clamp01(captureElapsed / captureSeconds);
    public bool PlayerInArea => player != null && operations != null
        && NullLabLayout.Contains(operations.InverseTransformPoint(player.PlayerCamera.transform.position));
    public float Residual => Mathf.Sqrt(Mathf.Max(0f, IncomingAmplitude * IncomingAmplitude + Amplitude * Amplitude
        + 2f * IncomingAmplitude * Amplitude * Mathf.Cos((IncomingPhase - PhaseDegrees) * Mathf.Deg2Rad)));
    public bool Balanced => Powered && Residual <= 0.025f;
    public string ObjectiveTitle => Certified ? "N-01 / NULL REFERENCE CERTIFIED"
        : !Powered ? "N-01 / ENERGIZE THE REFERENCE BENCH" : "N-01 / FIND THE QUIETEST SIGNAL";
    public string Guidance => Certified ? "The reference is stored. Explore the null cell through the opening beside the glass. R at the bench starts another test."
        : !Powered ? "Descend Stair 01. Follow the cable gallery to the supply cabinet and press F, then sit at the lab bench."
        : "At the bench: A / D phase, W / S amplitude. Flatten the white residual trace, then hold SPACE. Shift gives fine control.";
    public string Measurement => !Powered ? "BENCH SUPPLY ISOLATED / SAFETY LIGHTING ON"
        : $"RESIDUAL {Residual:0.000}  /  " + (Certified ? "REFERENCE STORED" : Balanced ? "NULL FOUND" : "BALANCING");
    public string Notes => "NULL REFERENCE LABORATORY / LEVEL 01\n\n"
        + "The chamber above removes echoes. Here, two signals remove each other.\n\n"
        + "AMBER: incoming reference. TEAL: your cancellation signal. WHITE: their sum. Make the white trace as flat as possible.\n\n"
        + "Match the two amplitudes and place their peaks opposite each other. The fixed reference is 0.65 at +55 degrees; its opposite phase is -125 degrees.\n\n"
        + Guidance + "\n\n" + Measurement + "\n\nTAB closes notes. Stair 01 returns to the chamber hallway.";

    public void Configure(FirstPersonPlayerController controller, Transform facility, SimpleSeatedConsoleController seat,
        Transform supply, Transform handle, Text label, Text display, Text status, Renderer screen,
        Transform phase, Transform amplitude, Transform assembly, Light[] lights)
    {
        player = controller;
        operations = facility;
        console = seat;
        supplyPoint = supply;
        supplyHandle = handle;
        supplyLabel = label;
        readout = display;
        cellStatus = status;
        scope = screen;
        phaseKnob = phase;
        amplitudeKnob = amplitude;
        rotor = assembly;
        taskLights = lights;
    }

    private void Awake()
    {
        effects = player.GetComponent<FacilityPlayerEffects>();
        trace = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        { name = "Null lab cancellation trace", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        pixels = new Color32[Width * Height];
        scopeProperties = new MaterialPropertyBlock();
        scope.GetPropertyBlock(scopeProperties);
        scopeProperties.SetTexture("_BaseMap", trace);
        scope.SetPropertyBlock(scopeProperties);
        tone = gameObject.AddComponent<AudioSource>();
        tone.playOnAwake = false;
        tone.loop = true;
        tone.spatialBlend = 0f;
        tone.volume = 0f;
        const int rate = 22050;
        float[] samples = new float[rate * 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = Mathf.Sin(i * 2f * Mathf.PI * 165f / rate) * 0.16f
                + Mathf.Sin(i * 2f * Mathf.PI * 330f / rate) * 0.04f;
        toneClip = AudioClip.Create("Null bench residual tone", samples.Length, 1, rate, false);
        toneClip.SetData(samples, 0);
        tone.clip = toneClip;
        tone.Play();
        UpdateDisplays();
        DrawTrace();
    }

    private bool AtSupply => player.enabled && Vector3.Distance(player.transform.position, supplyPoint.position) < 1.35f;

    private void Update()
    {
        if (RuntimeSceneSwitcher.IsOpen) return;
        Keyboard keys = Keyboard.current;
        if (keys != null && AtSupply && keys.fKey.wasPressedThisFrame)
        {
            Powered = !Powered;
            captureElapsed = 0f;
            if (effects != null) effects.PlayConfirmation();
        }
        if (Powered && console.IsSeated && keys != null)
        {
            if (Certified && keys.rKey.wasPressedThisFrame)
            {
                Certified = false;
                PhaseDegrees = 0f;
                Amplitude = 1f;
            }
            if (!Certified)
            {
                float fine = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed ? 0.2f : 1f;
                float phase = (keys.dKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed ? 1f : 0f);
                float gain = (keys.wKey.isPressed ? 1f : 0f) - (keys.sKey.isPressed ? 1f : 0f);
                PhaseDegrees = Mathf.Clamp(PhaseDegrees + phase * fine * 40f * Time.deltaTime, -180f, 180f);
                Amplitude = Mathf.Clamp(Amplitude + gain * fine * 0.25f * Time.deltaTime, 0f, 1.2f);
                captureElapsed = Balanced && keys.spaceKey.isPressed ? captureElapsed + Time.deltaTime : 0f;
                if (captureElapsed >= captureSeconds)
                {
                    Certified = true;
                    captureElapsed = 0f;
                    if (effects != null) effects.PlayConfirmation();
                }
            }
        }
        else captureElapsed = 0f;
        float distance = Vector3.Distance(player.PlayerCamera.transform.position, scope.transform.position);
        float volume = Powered && PlayerInArea ? Mathf.Clamp01(1f - distance / 11f) * Mathf.Clamp01(Residual) * 0.22f : 0f;
        tone.volume = Mathf.MoveTowards(tone.volume, volume, Time.deltaTime * 0.3f);
        UpdateDisplays();
        if (Time.time >= nextTraceTime)
        {
            nextTraceTime = Time.time + 0.08f;
            DrawTrace();
        }
    }

    private void UpdateDisplays()
    {
        supplyHandle.localRotation = Quaternion.Euler(0f, 0f, Powered ? -45f : 45f);
        supplyLabel.text = "N-01 / BENCH SUPPLY\n" + (Powered ? "ENERGIZED\nF / ISOLATE" : "ISOLATED\nF / ENERGIZE");
        readout.text = !Powered ? "NULL REFERENCE / BENCH ISOLATED\nSupply cabinet at the end of the cable gallery"
            : $"PHASE {PhaseDegrees:+0.0;-0.0;0.0} / AMP {Amplitude:0.00}\n"
                + (Certified ? "NULL STORED / R TO REPEAT" : $"RESIDUAL {Residual:0.000} / " + (Balanced ? "HOLD SPACE" : "BALANCE TO ZERO"));
        cellStatus.text = !Powered ? "NULL CELL / STANDBY" : Certified ? "NULL ACCEPTED / REFERENCE STORED" : "REFERENCE ACTIVE / BALANCE REQUIRED";
        phaseKnob.localRotation = Quaternion.Euler(0f, 0f, -PhaseDegrees);
        amplitudeKnob.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(135f, -135f, Amplitude / 1.2f));
        rotor.localRotation = Quaternion.Euler(0f, 0f, PhaseDegrees);
        foreach (Light light in taskLights)
        {
            light.enabled = Powered;
            light.color = Certified ? new Color(0.34f, 1f, 0.72f) : new Color(0.50f, 0.80f, 1f);
        }
    }

    private void LateUpdate()
    {
        if (AtSupply) InteractionPromptDisplay.Show(this, Powered ? "Press F to isolate the bench supply" : "Press F to energize the null reference bench");
        else InteractionPromptDisplay.Hide(this);
    }

    private void DrawTrace()
    {
        System.Array.Fill(pixels, new Color32(2, 12, 13, 255));
        Color32 grid = new(14, 43, 43, 255);
        for (int x = 16; x < Width - 16; x += 40) Vertical(x, 16, Height - 17, grid);
        for (int y = 16; y < Height - 16; y += 28)
            for (int x = 16; x < Width - 16; x++) pixels[y * Width + x] = grid;
        if (Powered)
        {
            int[] previous = { 0, 0, 0 };
            Color32[] colors = { new(237, 166, 69, 255), new(57, 208, 185, 255), new(222, 251, 244, 255) };
            for (int x = 16; x < Width - 16; x++)
            {
                float angle = (x - 16f) / (Width - 32f) * Mathf.PI * 6f + Time.time * 0.5f;
                float incoming = IncomingAmplitude * Mathf.Sin(angle + IncomingPhase * Mathf.Deg2Rad);
                float cancel = Amplitude * Mathf.Sin(angle + PhaseDegrees * Mathf.Deg2Rad);
                for (int line = 0; line < 3; line++)
                {
                    float value = line == 0 ? incoming : line == 1 ? cancel : incoming + cancel;
                    int y = Mathf.Clamp(Mathf.RoundToInt(128f + value * 56f), 17, Height - 18);
                    if (x > 16) Vertical(x, previous[line], y, colors[line]);
                    previous[line] = y;
                }
            }
        }
        trace.SetPixels32(pixels);
        trace.Apply(false);
    }

    private void Vertical(int x, int start, int end, Color32 color)
    {
        for (int y = Mathf.Min(start, end); y <= Mathf.Max(start, end); y++) pixels[y * Width + x] = color;
    }

    private void OnDisable() => InteractionPromptDisplay.Hide(this);
    private void OnDestroy()
    {
        if (trace != null) Destroy(trace);
        if (toneClip != null) Destroy(toneClip);
    }
}
