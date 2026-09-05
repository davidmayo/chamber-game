using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A fictional archive turns three imagined telemetry recordings into light and
// sound. The supplies, playback, and session record are deliberately simple.
public sealed class SignalArchiveController : MonoBehaviour
{
    private static readonly string[] Programs = { "ORBITAL", "PULSAR", "AURORA" };
    private static readonly string[] Receivers = { "TRACK", "TIMING", "FIELD" };
    private static readonly Color[] ProgramColors =
    {
        new(0.24f, 0.82f, 1f), new(1f, 0.59f, 0.25f), new(0.59f, 0.42f, 1f),
    };
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    [SerializeField] private FirstPersonPlayerController player;
    [SerializeField] private Transform operations;
    [SerializeField] private SimpleSeatedConsoleController console;
    [SerializeField] private Transform[] relayPoints;
    [SerializeField] private Text[] relayLabels;
    [SerializeField] private Renderer[] relayIndicators;
    [SerializeField] private Light[] accentLights;
    [SerializeField] private Text consoleReadout;
    [SerializeField] private SignalArchiveSculpture sculpture;
    [SerializeField, Min(0.1f)] private float performanceSeconds = 18f;
    private readonly bool[] powered = new bool[3];
    private readonly AudioClip[] recordings = new AudioClip[3];
    private readonly string[] receiverPrompts = new string[3];
    private MaterialPropertyBlock indicatorProperties;
    private float[] lightIntensities;
    private AudioSource sound;
    private FacilityPlayerEffects effects;
    private float activation;
    private float playbackElapsed;
    private int completedPrograms;
    private int displayedReceivers = -1;
    private int displayedConsoleState = -1;

    public int PoweredCount => (powered[0] ? 1 : 0) + (powered[1] ? 1 : 0) + (powered[2] ? 1 : 0);
    public bool Ready => PoweredCount == 3;
    public int SelectedProgram { get; private set; }
    public bool IsPerforming { get; private set; }
    public float PlaybackProgress01 => Mathf.Clamp01(playbackElapsed / Mathf.Max(0.1f, performanceSeconds));
    public float CaptureProgress01 => PlaybackProgress01;
    public int CompletedProgramCount => ((completedPrograms & 1) != 0 ? 1 : 0)
        + ((completedPrograms & 2) != 0 ? 1 : 0) + ((completedPrograms & 4) != 0 ? 1 : 0);
    public bool PlayerInArea => player != null && player.PlayerCamera != null && operations != null
        && SignalArchiveLayout.Contains(operations.InverseTransformPoint(player.PlayerCamera.transform.position));
    public string ProgramName => Programs[SelectedProgram];
    public string ObjectiveTitle => "AFTERGLOW / " + (IsPerforming ? ProgramName + " PLAYING"
        : !Ready ? "WAKE THE SIGNAL ARCHIVE" : CompletedProgramCount == 3 ? "ARCHIVE RESTORED" : "CHOOSE A RECORDING");
    public string Guidance => !Ready ? "Walk around the pool and energize the three receiver banks with F. The playback bench is beside the entrance."
        : IsPerforming ? "The recording is unfolding. F or Escape leaves the bench; walk around the light as it plays."
        : "At the bench, A / D chooses ORBITAL, PULSAR, or AURORA. Press SPACE once to play the complete recording.";
    public string Measurement => $"RECEIVERS {PoweredCount}/3   /   RECORDINGS {CompletedProgramCount}/3"
        + (IsPerforming ? $"   /   {PlaybackProgress01:P0}" : "");
    public string Notes => "SIGNAL ARCHIVE / AFTERGLOW / LEVEL 01\n\n"
        + "A fictional telemetry light sculpture beneath the DOC. These recordings are imagined patterns, not scientific measurements.\n\n"
        + "TRACK reveals the orbits. TIMING brings their moving points into view. FIELD completes the woven light.\n\n"
        + "ORBITAL: intersecting paths. PULSAR: a luminous hourglass. AURORA: a slowly folding curtain.\n\n"
        + Guidance + "\n\n" + Measurement + "\n\nProgress lasts for this session. TAB closes notes. Return through the gallery to Stair 01.";

    public void Configure(FirstPersonPlayerController controller, Transform facility, SimpleSeatedConsoleController seat,
        Transform[] receivers, Text[] labels, Renderer[] indicators, Light[] lights, Text display, SignalArchiveSculpture lightSculpture)
    {
        player = controller;
        operations = facility;
        console = seat;
        relayPoints = receivers;
        relayLabels = labels;
        relayIndicators = indicators;
        accentLights = lights;
        consoleReadout = display;
        sculpture = lightSculpture;
    }

    private void Awake()
    {
        if (player == null || operations == null || console == null || sculpture == null
            || relayPoints == null || relayPoints.Length != 3)
        {
            Debug.LogError("Signal archive is missing its player, bench, sculpture, or three receiver points.", this);
            enabled = false;
            return;
        }
        effects = player.GetComponent<FacilityPlayerEffects>();
        indicatorProperties = new MaterialPropertyBlock();
        lightIntensities = new float[accentLights != null ? accentLights.Length : 0];
        for (int i = 0; i < lightIntensities.Length; i++)
            lightIntensities[i] = accentLights[i] != null ? accentLights[i].intensity : 0f;
        sound = gameObject.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        sound.loop = true;
        sound.spatialBlend = 0f;
        sound.volume = 0f;
        UpdatePresentation();
    }

    private int NearbyReceiver()
    {
        if (player == null || !player.enabled || relayPoints == null) return -1;
        for (int i = 0; i < relayPoints.Length; i++)
            if (relayPoints[i] != null && Vector3.Distance(player.transform.position, relayPoints[i].position) < 1.15f)
                return i;
        return -1;
    }

    private void Update()
    {
        if (RuntimeSceneSwitcher.IsOpen) return;
        Keyboard keys = Keyboard.current;
        if (keys != null)
        {
            int receiver = NearbyReceiver();
            if (receiver >= 0 && keys.fKey.wasPressedThisFrame)
            {
                powered[receiver] = !powered[receiver];
                if (!Ready)
                {
                    IsPerforming = false;
                    playbackElapsed = 0f;
                }
                if (effects != null) effects.PlayConfirmation();
            }
            if (console.IsSeated && !IsPerforming)
            {
                int direction = (keys.dKey.wasPressedThisFrame ? 1 : 0) - (keys.aKey.wasPressedThisFrame ? 1 : 0);
                if (direction != 0)
                {
                    SelectedProgram = (SelectedProgram + direction + Programs.Length) % Programs.Length;
                    playbackElapsed = 0f;
                }
                if (Ready && keys.spaceKey.wasPressedThisFrame)
                {
                    playbackElapsed = 0f;
                    IsPerforming = true;
                    // Generate each recording only on its first playback. An
                    // unexplored archive allocates no audio sample buffers.
                    if (recordings[SelectedProgram] == null)
                        recordings[SelectedProgram] = CreateRecording(SelectedProgram);
                    sound.clip = recordings[SelectedProgram];
                    sound.Play();
                }
            }
        }
        if (IsPerforming)
        {
            playbackElapsed = Mathf.Min(playbackElapsed + Time.deltaTime, performanceSeconds);
            if (playbackElapsed >= performanceSeconds)
            {
                IsPerforming = false;
                completedPrograms |= 1 << SelectedProgram;
                if (PlayerInArea && effects != null) effects.PlayConfirmation();
            }
        }
        activation = Mathf.MoveTowards(activation, PoweredCount / 3f, Time.deltaTime * 0.5f);
        UpdatePresentation();
        UpdateSound();
    }

    private void UpdatePresentation()
    {
        sculpture.SetPresentation(activation, SelectedProgram, PlaybackProgress01, IsPerforming);
        float envelope = IsPerforming ? Mathf.Sin(PlaybackProgress01 * Mathf.PI) : 0f;
        for (int i = 0; i < lightIntensities.Length; i++)
        {
            Light light = accentLights[i];
            if (light == null) continue;
            light.intensity = lightIntensities[i] * Mathf.Lerp(0.16f, 0.72f + envelope * 0.28f, activation);
            light.color = Color.Lerp(light.color, ProgramColors[SelectedProgram], Time.deltaTime * 1.5f);
        }
        UpdateDisplays();
    }

    private void UpdateDisplays()
    {
        int receiverMask = (powered[0] ? 1 : 0) | (powered[1] ? 2 : 0) | (powered[2] ? 4 : 0);
        for (int i = 0; i < powered.Length; i++)
        {
            // Each supply updates its text and property block only when toggled.
            if (displayedReceivers >= 0 && ((displayedReceivers ^ receiverMask) & (1 << i)) == 0) continue;
            receiverPrompts[i] = $"Press F to {(powered[i] ? "isolate" : "energize")} receiver {i + 1:00} / {Receivers[i]}";
            if (relayLabels != null && i < relayLabels.Length && relayLabels[i] != null)
                relayLabels[i].text = $"RECEIVER {i + 1:00} / {Receivers[i]}\n"
                    + (powered[i] ? "ONLINE / F TO ISOLATE" : "ISOLATED / F TO ENERGIZE");
            if (relayIndicators == null || i >= relayIndicators.Length || relayIndicators[i] == null) continue;
            Color color = powered[i] ? new Color(0.1f, 0.9f, 0.8f) : new Color(0.35f, 0.13f, 0.035f);
            relayIndicators[i].GetPropertyBlock(indicatorProperties);
            indicatorProperties.SetColor(BaseColor, color);
            indicatorProperties.SetColor(EmissionColor, color * (powered[i] ? 2f : 0.35f));
            relayIndicators[i].SetPropertyBlock(indicatorProperties);
        }
        displayedReceivers = receiverMask;
        // The readout shows whole percentages, so intermediate animation frames
        // need neither string formatting nor a world-space Canvas rebuild.
        int percentage = IsPerforming ? Mathf.RoundToInt(PlaybackProgress01 * 100f) : 0;
        int displayState = receiverMask | (SelectedProgram << 3) | ((IsPerforming ? 1 : 0) << 5)
            | (completedPrograms << 6) | (percentage << 9);
        if (displayState == displayedConsoleState) return;
        displayedConsoleState = displayState;
        if (consoleReadout != null)
            consoleReadout.text = "AFTERGLOW / SIGNAL ARCHIVE\n"
                + (Ready ? $"{SelectedProgram + 1:00} / {ProgramName}" : $"RECEIVERS {PoweredCount}/3 / ENERGIZE ALL THREE BANKS")
                + "\n" + (IsPerforming ? $"PLAYING {PlaybackProgress01:P0} / F TO EXPLORE"
                    : Ready ? $"A / D SELECT / SPACE PLAY / {CompletedProgramCount}/3 RECOVERED" : "WALK THE PERIMETER / F AT EACH RECEIVER");
    }

    private void UpdateSound()
    {
        if (!PlayerInArea)
        {
            sound.volume = 0f;
            if (!IsPerforming && sound.isPlaying) sound.Stop();
            return;
        }
        float envelope = Mathf.Sin(PlaybackProgress01 * Mathf.PI);
        float volume = IsPerforming ? 0.2f + envelope * 0.16f : 0f;
        sound.volume = Mathf.MoveTowards(sound.volume, volume, Time.deltaTime * 0.8f);
        if (!IsPerforming && sound.volume <= 0f && sound.isPlaying) sound.Stop();
    }

    private static AudioClip CreateRecording(int program)
    {
        const int rate = 22050;
        const int seconds = 6;
        float[] samples = new float[rate * seconds];
        float[] notes = program == 0 ? new[] { 130.81f, 164.81f, 196f }
            : program == 1 ? new[] { 146.83f, 174.61f, 220f } : new[] { 110f, 130.81f, 164.81f };
        // Integer cycles over the loop keep the generated pad seamless. A slow
        // swell replaces percussion; no samples, streaming, or audio thread code.
        for (int sample = 0; sample < samples.Length; sample++)
        {
            float fraction = sample / (float)samples.Length;
            float value = 0f;
            for (int note = 0; note < notes.Length; note++)
            {
                float angle = fraction * Mathf.PI * 2f * Mathf.Round(notes[note] * seconds);
                float swell = 0.75f + 0.25f * Mathf.Sin(fraction * Mathf.PI * 2f + note * 2f);
                value += (Mathf.Sin(angle) * 0.12f + Mathf.Sin(angle * 2f) * 0.018f) * swell;
            }
            samples[sample] = value;
        }
        AudioClip clip = AudioClip.Create("Archive harmonic recording / " + Programs[program], samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void LateUpdate()
    {
        int receiver = RuntimeSceneSwitcher.IsOpen ? -1 : NearbyReceiver();
        if (receiver >= 0) InteractionPromptDisplay.Show(this, receiverPrompts[receiver]);
        else InteractionPromptDisplay.Hide(this);
    }

    private void OnDisable()
    {
        InteractionPromptDisplay.Hide(this);
        if (sound != null) sound.Stop();
    }

    private void OnDestroy()
    {
        if (sound != null) sound.Stop();
        foreach (AudioClip clip in recordings)
            if (clip != null) Destroy(clip);
    }
}
