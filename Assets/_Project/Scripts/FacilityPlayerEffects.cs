using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class FacilityPlayerEffects : MonoBehaviour
{
    [SerializeField] private FirstPersonPlayerController player;
    [SerializeField] private Light inspectionLight;
    [SerializeField] private Transform groundOpsRoot;
    private CharacterController body;
    private AudioSource footsteps;
    private AudioSource roomHum;
    private AudioSource wind;
    private AudioSource confirmations;
    private AudioClip stepClip;
    private AudioClip humClip;
    private AudioClip windClip;
    private AudioClip confirmationClip;
    private UniversalAdditionalLightData lightData;
    private Vector3 previousPosition;
    private float distanceSinceStep;
    private int stepIndex;
    private uint currentMask;

    public bool FlashlightOn => inspectionLight != null && inspectionLight.enabled;
    public string LocationName { get; private set; } = "FACILITY";

    public void Configure(FirstPersonPlayerController controller, Light beam, Transform operations)
    {
        player = controller;
        inspectionLight = beam;
        groundOpsRoot = operations;
    }

    private void Awake()
    {
        if (player == null || inspectionLight == null) { enabled = false; return; }
        body = player.GetComponent<CharacterController>();
        inspectionLight.enabled = false;
        lightData = inspectionLight.GetUniversalAdditionalLightData();
        previousPosition = player.transform.position;
        stepClip = MakeClip("Soft work boots", 0.2f, StepSample);
        humClip = MakeClip("Facility ventilation", 4f, HumSample);
        windClip = MakeClip("Ridge wind", 4f, WindSample);
        confirmationClip = MakeClip("Measurement accepted", 0.4f, ConfirmationSample);
        footsteps = NewSource("Footsteps", stepClip, false);
        roomHum = NewSource("Room ventilation", humClip, true);
        wind = NewSource("Exterior wind", windClip, true);
        confirmations = NewSource("Instrument confirmation", confirmationClip, false);
    }

    private AudioSource NewSource(string name, AudioClip clip, bool loop)
    {
        GameObject item = new(name);
        item.transform.SetParent(transform, false);
        AudioSource source = item.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = loop;
        source.clip = clip;
        source.volume = 0f;
        if (loop) source.Play();
        return source;
    }

    private void Update()
    {
        Vector3 position = player.transform.position;
        float moved = Vector3.Distance(position, previousPosition);
        previousPosition = position;
        if (RuntimeSceneSwitcher.IsOpen) return;

        // The light is a standing inspection tool. Seated consoles keep their
        // own fixed practical lighting and camera controls.
        if (player.enabled && Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            inspectionLight.enabled = !inspectionLight.enabled;
        if (!player.enabled) inspectionLight.enabled = false;

        uint mask = Locate(player.PlayerCamera.transform.position);
        if (mask != currentMask)
        {
            currentMask = mask;
            inspectionLight.renderingLayerMask = (int)mask;
            lightData.renderingLayers = (RenderingLayerMask)mask;
            lightData.shadowRenderingLayers = (RenderingLayerMask)mask;
        }
        float humLevel = mask == (1u << 2) ? (LocationName == "SERVER ROOM" ? 0.12f : 0.055f)
            : mask == (1u << 3) ? 0.025f : 0f;
        float windLevel = mask == (1u << 1) ? 0.14f : 0f;
        roomHum.volume = Mathf.MoveTowards(roomHum.volume, humLevel, Time.deltaTime * 0.12f);
        wind.volume = Mathf.MoveTowards(wind.volume, windLevel, Time.deltaTime * 0.12f);

        if (!player.enabled || !body.isGrounded || moved > 1f)
        {
            distanceSinceStep = 0f;
            return;
        }
        distanceSinceStep += moved;
        if (moved > 0.001f && distanceSinceStep >= 1.35f)
        {
            distanceSinceStep = 0f;
            footsteps.volume = mask == (1u << 5) ? 0.12f : mask == (1u << 1) ? 0.32f : 0.22f;
            footsteps.pitch = 0.94f + (stepIndex++ % 3) * 0.055f;
            footsteps.Play();
        }
    }

    private uint Locate(Vector3 position)
    {
        Vector3 local = groundOpsRoot != null ? groundOpsRoot.InverseTransformPoint(position) : position;
        if (SignalArchiveLayout.Contains(local))
        {
            LocationName = "LEVEL 01 / SIGNAL ARCHIVE";
            return SignalArchiveLayout.RenderingLayer;
        }
        if (NullLabLayout.Contains(local))
        {
            LocationName = NullLabLayout.Location(local);
            return NullLabLayout.RenderingLayer;
        }
        float halfWidth = position.z >= 0f ? 2.5f : Mathf.Lerp(2.5f, 0.5f, -position.z / 5f);
        if (position.y >= 0f && position.y <= 3.6f && position.z >= -5f && position.z <= 5f
            && Mathf.Abs(position.x) <= halfWidth)
        {
            LocationName = "ANECHOIC CHAMBER";
            return 1u << 5;
        }
        if (position.y >= -0.2f && position.y <= 5.5f && Mathf.Abs(position.x) <= 4.5f
            && position.z >= -9f && position.z <= 8f)
        {
            LocationName = "CHAMBER CONTAINING ROOM";
            return 1u << 4;
        }
        if (local.y >= 0f && local.y <= 4f)
        {
            if ((local.x >= 5.5f && local.x <= 8.2f && local.z >= -8.2f && local.z <= 27.5f)
                || (local.x >= -4.3f && local.x <= 8.2f && local.z >= -8.2f && local.z <= -5.5f))
            {
                LocationName = "FACILITY HALLWAY";
                return 1u << 3;
            }
            if (local.x >= -8.5f && local.x <= 5.5f && local.z >= -5.5f && local.z <= 8f)
            {
                LocationName = local.z > 4.5f ? "SERVER ROOM" : "DISH OPERATIONS";
                return 1u << 2;
            }
        }
        LocationName = "ANTENNA RIDGE / EXTERIOR";
        return 1u << 1;
    }

    public void PlayConfirmation()
    {
        if (confirmations == null) return;
        confirmations.volume = 0.22f;
        confirmations.Play();
    }

    private static AudioClip MakeClip(string name, float seconds, Func<float, float, float> sample)
    {
        const int rate = 22050;
        float[] data = new float[Mathf.CeilToInt(seconds * rate)];
        System.Random random = new(1707);
        float noise = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2f - 1f, 0.1f);
            data[i] = Mathf.Clamp(sample(i / (float)rate, noise), -1f, 1f);
        }
        // Looping ambiences fade gently through their boundary without clicks.
        if (seconds > 1f)
            for (int i = 0; i < data.Length; i++)
                data[i] *= Mathf.Min(1f, i / 1100f, (data.Length - 1 - i) / 1100f);
        AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float StepSample(float t, float noise) =>
        (Mathf.Sin(t * 2f * Mathf.PI * 85f) * 0.35f + noise * 1.1f) * Mathf.Exp(-28f * t)
        * Mathf.Min(1f, t * 500f);
    private static float HumSample(float t, float noise) =>
        Mathf.Sin(t * 2f * Mathf.PI * 60f) * 0.16f + Mathf.Sin(t * 2f * Mathf.PI * 120f) * 0.045f + noise * 0.18f;
    private static float WindSample(float t, float noise) => noise * (0.7f + 0.3f * Mathf.Sin(t * Mathf.PI * 0.5f));
    private static float ConfirmationSample(float t, float noise) =>
        Mathf.Sin(t * 2f * Mathf.PI * (t < 0.16f ? 660f : 880f)) * 0.25f
        * Mathf.Sin(Mathf.Clamp01(t / 0.4f) * Mathf.PI);

    private void OnDestroy()
    {
        if (stepClip != null) Destroy(stepClip);
        if (humClip != null) Destroy(humClip);
        if (windClip != null) Destroy(windClip);
        if (confirmationClip != null) Destroy(confirmationClip);
    }
}
