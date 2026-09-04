using UnityEngine;
using UnityEngine.InputSystem;

public sealed class FacilityShiftController : MonoBehaviour
{
    public enum ShiftStage { ChamberReference, SatelliteAcquisition, RidgeSnapshot, FileReport, Complete }

    [SerializeField] private ChamberReferenceSignal reference;
    [SerializeField] private ComputerConsoleController chamberConsole;
    [SerializeField] private GroundOpsDishConsoleController dishConsole;
    [SerializeField] private SimpleSeatedConsoleController rackConsole;
    [SerializeField] private GroundOpsDishController dishes;
    [SerializeField] private GroundOpsSatelliteTarget target;
    [SerializeField] private RidgeRecorderController recorder;
    [SerializeField] private FacilityPlayerEffects playerEffects;
    [SerializeField, Min(0.1f)] private float captureSeconds = 2f;

    private float captureElapsed;
    private float shiftSeconds;
    private float completedSeconds;
    private float referencePower;
    private float pointingError;

    public ShiftStage Stage { get; private set; }
    public float CaptureProgress01 => Mathf.Clamp01(captureElapsed / captureSeconds);
    public float ElapsedSeconds => Stage == ShiftStage.Complete ? completedSeconds : shiftSeconds;
    public GroundOpsSatelliteTarget Target => target;
    public bool NotebookOpen { get; private set; }
    public string LocationName => playerEffects != null ? playerEffects.LocationName : "FACILITY";
    public bool FlashlightOn => playerEffects != null && playerEffects.FlashlightOn;
    public float LastReferencePower => referencePower;
    public float LastPointingError => pointingError;
    public string LatestEntry { get; private set; } = "The site is quiet. Prove the signal chain before handover.";

    public void Configure(ChamberReferenceSignal signal, ComputerConsoleController chamber,
        GroundOpsDishConsoleController hardware, SimpleSeatedConsoleController racks,
        GroundOpsDishController antennas, GroundOpsSatelliteTarget satellite,
        RidgeRecorderController ridge, FacilityPlayerEffects effects)
    {
        reference = signal;
        chamberConsole = chamber;
        dishConsole = hardware;
        rackConsole = racks;
        dishes = antennas;
        target = satellite;
        recorder = ridge;
        playerEffects = effects;
    }

    public float DishErrorDegrees
    {
        get
        {
            if (dishes == null || target == null) return 180f;
            return Vector3.Angle(Direction(dishes.AzimuthDegrees, dishes.ElevationDegrees),
                Direction(target.AzimuthDegrees, target.ElevationDegrees));
        }
    }

    private static Vector3 Direction(float azimuth, float elevation)
    {
        float az = azimuth * Mathf.Deg2Rad;
        float el = elevation * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
    }

    private void Update()
    {
        if (RuntimeSceneSwitcher.IsOpen) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) NotebookOpen = !NotebookOpen;
        if (Stage != ShiftStage.Complete) shiftSeconds += Time.deltaTime;
        if (reference == null || target == null || recorder == null) return;

        if (Stage == ShiftStage.Complete)
        {
            if (rackConsole.IsSeated && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                RestartShift();
            }
            return;
        }
        if (Stage == ShiftStage.RidgeSnapshot)
        {
            if (recorder.HasSnapshot)
                Advance("Ridge snapshot secured. The last check is back at the DSN racks.");
            return;
        }

        bool ready = Stage switch
        {
            ShiftStage.ChamberReference => chamberConsole.IsSeated && reference.IsAligned,
            ShiftStage.SatelliteAcquisition => dishConsole.IsSeated && DishErrorDegrees <= 1f,
            ShiftStage.FileReport => rackConsole.IsSeated,
            _ => false,
        };
        if (!ready || keyboard == null || !keyboard.spaceKey.isPressed)
        {
            captureElapsed = 0f;
            return;
        }
        captureElapsed += Time.deltaTime;
        if (captureElapsed < captureSeconds) return;

        switch (Stage)
        {
            case ShiftStage.ChamberReference:
                referencePower = reference.PowerDbm;
                Advance("Chamber reference captured. Follow the hallway to Dish Operations.");
                break;
            case ShiftStage.SatelliteAcquisition:
                pointingError = DishErrorDegrees;
                Advance($"{target.TargetName} acquired. Take the truck to collect the local recorder snapshot.");
                break;
            case ShiftStage.FileReport:
                completedSeconds = shiftSeconds;
                Advance("Signal chain verified. Handover filed. The facility is yours to explore.");
                NotebookOpen = true;
                break;
        }
    }

    private void Advance(string entry)
    {
        Stage++;
        captureElapsed = 0f;
        LatestEntry = entry;
        if (playerEffects != null) playerEffects.PlayConfirmation();
    }

    public void RestartShift()
    {
        Stage = ShiftStage.ChamberReference;
        captureElapsed = 0f;
        shiftSeconds = 0f;
        referencePower = 0f;
        pointingError = 0f;
        recorder.ResetSnapshot();
        LatestEntry = "New verification started. Return to the chamber for a fresh reference.";
        NotebookOpen = false;
    }

    public string ObjectiveTitle => Stage switch
    {
        ShiftStage.ChamberReference => "01  /  CAPTURE THE CHAMBER REFERENCE",
        ShiftStage.SatelliteAcquisition => "02  /  ACQUIRE THE SATELLITE",
        ShiftStage.RidgeSnapshot => "03  /  COLLECT THE RIDGE SNAPSHOT",
        ShiftStage.FileReport => "04  /  FILE THE HANDOVER",
        _ => "SHIFT COMPLETE  /  SIGNAL CHAIN VERIFIED",
    };

    public string Guidance => Stage switch
    {
        ShiftStage.ChamberReference => "Chamber computer: pan +30, tilt -10, polarity 90.\nMatch the reference, then hold SPACE to capture.",
        ShiftStage.SatelliteAcquisition => target == null ? "Find the hardware-control station in the DOC."
            : $"DOC hardware station: {target.TargetName}\nAzimuth {target.AzimuthDegrees:0.0} / elevation {target.ElevationDegrees:0.0}. Hold SPACE within 1 degree.",
        ShiftStage.RidgeSnapshot => "Follow RIDGE ACCESS to the truck. F enters; tap W to depart.\nAt the ridge, find Recorder 07 and press F to copy its buffer.",
        ShiftStage.FileReport => "Return by truck. Sit at the DSN racks in the server room.\nHold SPACE to verify the snapshot and file your report.",
        _ => "Keep exploring, or sit at the DSN racks and press SPACE for another shift.",
    };

    public string Measurement => Stage switch
    {
        ShiftStage.ChamberReference => reference == null ? "" : $"REFERENCE  {reference.PowerDbm:0.0} dBm  /  "
            + (reference.IsAligned ? "MATCHED" : "TUNING"),
        ShiftStage.SatelliteAcquisition => $"POINTING ERROR  {DishErrorDegrees:0.00} degrees",
        ShiftStage.RidgeSnapshot => recorder != null && recorder.Progress01 > 0f
            ? $"RECORDER TRANSFER  {recorder.Progress01 * 100f:0}%" : "RECORDER 07  /  LOCAL ACCESS REQUIRED",
        ShiftStage.FileReport => "REFERENCE + SATELLITE + LOCAL SNAPSHOT READY",
        _ => $"REFERENCE {referencePower:0.0} dBm  /  POINTING ERROR {pointingError:0.00} degrees",
    };
}
