using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class SignalWatchTests : InputTestFixture
{
    private Keyboard keyboard;
    private Behaviour player;
    private float previousCaptureDelta;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.AddDevice<Mouse>();
        previousCaptureDelta = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 60f;
    }

    public override void TearDown()
    {
        Behaviour menu = Find("RuntimeSceneSwitcher");
        if (menu != null) UnityEngine.Object.DestroyImmediate(menu.gameObject);
        Time.captureDeltaTime = previousCaptureDelta;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        base.TearDown();
    }

    [UnityTest]
    public IEnumerator CompleteShiftUsesRealControlsAndTheRoundTrip()
    {
        yield return LoadFacility();
        Behaviour shift = Find("FacilityShiftController");
        Behaviour signal = Find("ChamberReferenceSignal");
        Behaviour table = Find("TurntableController");
        Behaviour source = Find("SourceAntennaController");
        Behaviour chamber = Find("ComputerConsoleController");
        Behaviour hardware = Find("GroundOpsDishConsoleController");
        Behaviour dishes = Find("GroundOpsDishController");
        Behaviour target = Find("GroundOpsSatelliteTarget");
        Behaviour rail = Find("RailTruckController");
        Behaviour recorder = Find("RidgeRecorderController");
        Behaviour racks = GameObject.Find("Ground Ops Blockout/Server Room Equipment/DSN Server Rack")
            .GetComponentInChildren(Type.GetType("SimpleSeatedConsoleController, Assembly-CSharp")) as Behaviour;
        Transform hardwareFurniture = hardware.transform.parent.Find("Furniture");
        foreach (string monitorName in new[] { "Left 27-inch Monitor", "Right 27-inch Monitor" })
        {
            Assert.That(hardwareFurniture.Find(monitorName).GetComponentsInChildren<Canvas>(), Has.Length.EqualTo(1),
                "Scene synchronization must not accumulate duplicate monitor canvases.");
        }
        Write(shift, "captureSeconds", 0.15f);
        Write(recorder, "downloadSeconds", 0.3f);
        Write(rail, "fadeHalfSeconds", 0.01f);
        Write(rail, "speedMetersPerSecond", 500f);
        foreach (Behaviour console in new[] { chamber, hardware, racks }) Write(console, "transitionSeconds", 0.01f);
        // Arrange the documented starting state, independently of Scene Tools.
        Call(table, "SetPose", 0f, 0f, 0.2f);
        Call(source, "SetPolarityDegrees", 0f);
        Call(dishes, "SetPose", 0f, 90f);

        yield return Approach(chamber);
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => Property<bool>(chamber, "IsSeated"));
        Press(keyboard.spaceKey);
        yield return Frames(15);
        Release(keyboard.spaceKey);
        Assert.That(Property<object>(shift, "Stage").ToString(), Is.EqualTo("ChamberReference"),
            "An unmatched reference must not be accepted.");
        float weakPower = Property<float>(signal, "PowerDbm");
        float height = Property<float>(table, "HeightMeters");
        yield return Tune(table, "PanDegrees", 30f, keyboard.dKey, keyboard.aKey);
        yield return Tune(table, "TiltDegrees", -10f, keyboard.sKey, keyboard.wKey);
        yield return Tune(source, "PolarityDegrees", 90f, keyboard.qKey, keyboard.eKey);
        Assert.That(Property<bool>(signal, "IsAligned"), Is.True);
        Assert.That(Property<float>(signal, "PowerDbm"), Is.GreaterThan(weakPower + 20f));
        Assert.That(Property<float>(table, "HeightMeters"), Is.EqualTo(height));
        Behaviour analyzer = Find("SpectrumAnalyzerDisplay");
        Texture2D liveTrace = Field(analyzer, "liveTrace") as Texture2D;
        Assert.That(liveTrace, Is.Not.Null, "The reference must drive a live analyzer trace.");
        Assert.That(liveTrace.width, Is.EqualTo(512));
        yield return CaptureImage("reference-matched");
        yield return Capture(shift, "SatelliteAcquisition");
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => player.enabled);

        yield return Approach(hardware);
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => Property<bool>(hardware, "IsSeated"));
        Press(keyboard.leftCtrlKey);
        yield return Tune(dishes, "AzimuthDegrees", Property<float>(target, "AzimuthDegrees"), keyboard.aKey, keyboard.dKey);
        yield return Tune(dishes, "ElevationDegrees", Property<float>(target, "ElevationDegrees"), keyboard.sKey, keyboard.wKey);
        Release(keyboard.leftCtrlKey);
        yield return null;
        Assert.That(Property<float>(shift, "DishErrorDegrees"), Is.LessThanOrEqualTo(1f));
        yield return Capture(shift, "RidgeSnapshot");
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => player.enabled);

        Transform departure = Field(rail, "departureInteractionPoint") as Transform;
        Transform ridgeExit = Field(rail, "antennaExitPose") as Transform;
        yield return MoveTo(departure.position);
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => Property<string>(rail, "StateName") == "ArrivedAtDoc");
        yield return Tap(keyboard.wKey);
        yield return Tap(keyboard.escapeKey);
        Assert.That(Time.timeScale, Is.Zero, "Escape must pause an automatic truck leg.");
        float truckProgress = Property<float>(rail, "Progress01");
        yield return Frames(8);
        Assert.That(Property<float>(rail, "Progress01"), Is.EqualTo(truckProgress));
        yield return Tap(keyboard.escapeKey);
        yield return WaitFor(() => Property<string>(rail, "StateName") == "ArrivedAtAntennas");
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => player.enabled);
        Transform reader = Property<Transform>(recorder, "InteractionPoint");
        Assert.That(Vector3.Distance(reader.position, ridgeExit.position), Is.GreaterThan(3.5f),
            "The recorder and truck must not compete for the same F press.");
        yield return WalkTo(reader.position);
        yield return CaptureImage("ridge-recorder");
        yield return Tap(keyboard.fKey);
        yield return Frames(3);
        yield return Tap(keyboard.escapeKey);
        float progress = Property<float>(recorder, "Progress01");
        yield return Frames(8);
        Assert.That(Property<float>(recorder, "Progress01"), Is.EqualTo(progress),
            "Pausing must freeze the recorder transfer.");
        yield return Tap(keyboard.escapeKey);
        yield return WaitFor(() => Property<bool>(recorder, "HasSnapshot"));
        yield return WaitFor(() => Property<object>(shift, "Stage").ToString() == "FileReport");

        yield return WalkTo(ridgeExit.position);
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => Property<string>(rail, "StateName") == "ArrivedAtAntennas");
        yield return Tap(keyboard.wKey);
        yield return WaitFor(() => Property<string>(rail, "StateName") == "ArrivedAtDoc");
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => player.enabled);
        yield return Approach(racks);
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => Property<bool>(racks, "IsSeated"));
        yield return Capture(shift, "Complete");
        Assert.That(Property<float>(shift, "LastReferencePower"), Is.GreaterThan(-50f));
        Assert.That(Property<float>(shift, "LastPointingError"), Is.LessThanOrEqualTo(1f));
        Assert.That(Property<bool>(shift, "NotebookOpen"), Is.True);
        Text notebook = Find("FacilityShiftDisplay").transform.Find(
            "Signal Watch Canvas/Field Notebook/Notebook Entries").GetComponent<Text>();
        Assert.That(notebook.preferredHeight, Is.LessThanOrEqualTo(notebook.rectTransform.rect.height),
            "The completed report must fit inside the notebook.");
        yield return CaptureImage("shift-complete");
        yield return Tap(keyboard.spaceKey);
        Assert.That(Property<object>(shift, "Stage").ToString(), Is.EqualTo("ChamberReference"));
        Assert.That(Property<bool>(recorder, "HasSnapshot"), Is.False);
    }

    [UnityTest]
    public IEnumerator InspectionLightNotebookAndPauseRespectInputAndLightingZones()
    {
        yield return LoadFacility();
        Behaviour shift = Find("FacilityShiftController");
        Behaviour effects = Find("FacilityPlayerEffects");
        Light beam = Field(effects, "inspectionLight") as Light;
        yield return MoveTo(new Vector3(-1.5f, 0f, 3.5f));
        Assert.That(beam.enabled, Is.False, "The inspection light must start off.");
        yield return Tap(keyboard.lKey);
        Assert.That(beam.enabled, Is.True);
        Assert.That(beam.shadows, Is.EqualTo(LightShadows.Hard));
        Assert.That(beam.renderingLayerMask, Is.EqualTo(1 << 5));
        // Review the inspection beam in the chamber's intentional darkness.
        Behaviour chamberLights = Find("MotionSensitiveChamberLights");
        object off = Enum.Parse(chamberLights.GetType().GetProperty("Mode").PropertyType, "Off");
        Call(chamberLights, "SetMode", off);
        Call(Find("FloodLightController"), "SetLightsOn", false);
        player.transform.rotation = Quaternion.LookRotation(Vector3.back);
        yield return Frames(2);
        yield return CaptureImage("inspection-light");
        yield return Tap(keyboard.tabKey);
        Assert.That(Property<bool>(shift, "NotebookOpen"), Is.True);
        Canvas canvas = Find("FacilityShiftDisplay").GetComponentInChildren<Canvas>(true);
        Assert.That(canvas.GetComponent<CanvasScaler>().referenceResolution,
            Is.EqualTo(new Vector2(1920f, 1080f)));
        foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.False, "Field notes must not steal pointer input.");

        yield return Tap(keyboard.escapeKey);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(canvas.gameObject.activeSelf, Is.False);
        yield return Tap(keyboard.lKey);
        yield return Tap(keyboard.tabKey);
        Assert.That(beam.enabled, Is.True);
        Assert.That(Property<bool>(shift, "NotebookOpen"), Is.True);
        yield return Tap(keyboard.escapeKey);
        yield return Tap(keyboard.tabKey);
        Assert.That(Property<bool>(shift, "NotebookOpen"), Is.False);

        Transform operations = GameObject.Find("Ground Ops Blockout").transform;
        yield return MoveTo(operations.TransformPoint(new Vector3(1f, 0f, 1f)));
        Assert.That(beam.renderingLayerMask, Is.EqualTo(1 << 2));
        yield return MoveTo(operations.TransformPoint(new Vector3(6.8f, 0f, 6.25f)));
        Assert.That(beam.renderingLayerMask, Is.EqualTo(1 << 3));
        Assert.That(player.GetComponentsInChildren<AudioSource>(), Has.Length.EqualTo(4));
    }

    private IEnumerator LoadFacility()
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return Frames(2);
        player = Find("FirstPersonPlayerController");
        if (Find("RuntimeSceneSwitcher") == null)
            new GameObject("Signal Watch Test Pause Menu").AddComponent(Type.GetType("RuntimeSceneSwitcher, Assembly-CSharp"));
        Assert.That(Find("FacilityShiftController"), Is.Not.Null);
    }

    private IEnumerator Tune(Behaviour equipment, string property, float target, ButtonControl negative, ButtonControl positive)
    {
        int frames = 0;
        while (Mathf.Abs(Property<float>(equipment, property) - target) > 0.55f && frames++ < 500)
        {
            ButtonControl button = Property<float>(equipment, property) < target ? positive : negative;
            Press(button);
            yield return null;
            Release(button);
            yield return null;
        }
        Assert.That(Property<float>(equipment, property), Is.EqualTo(target).Within(0.6f), property);
    }

    private IEnumerator Capture(Behaviour shift, string nextStage)
    {
        Press(keyboard.spaceKey);
        yield return WaitFor(() => Property<object>(shift, "Stage").ToString() == nextStage);
        Release(keyboard.spaceKey);
        yield return null;
    }

    private IEnumerator Approach(Behaviour console) => MoveTo(new Vector3(
        console.GetComponent<BoxCollider>().bounds.center.x, 0f, console.GetComponent<BoxCollider>().bounds.center.z));

    private IEnumerator MoveTo(Vector3 position)
    {
        player.transform.position = position;
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return Frames(2);
    }

    private IEnumerator WalkTo(Vector3 destination)
    {
        int frames = 0;
        while (Vector3.ProjectOnPlane(destination - player.transform.position, Vector3.up).magnitude > 0.65f
               && frames++ < 360)
        {
            player.transform.rotation = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(destination - player.transform.position, Vector3.up));
            Press(keyboard.wKey);
            yield return null;
        }
        Release(keyboard.wKey);
        yield return Frames(2);
        Assert.That(Vector3.Distance(player.transform.position, destination), Is.LessThan(1.4f),
            $"The walking route to {destination} is blocked at {player.transform.position}.");
    }

    private IEnumerator Tap(ButtonControl key)
    {
        Press(key);
        yield return null;
        Release(key);
        yield return null;
    }

    private static IEnumerator WaitFor(Func<bool> condition)
    {
        int frames = 0;
        while (!condition() && frames++ < 600) yield return null;
        Assert.That(condition(), Is.True, "The expected gameplay state was not reached.");
        yield return null;
    }

    private static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }

    private static IEnumerator CaptureImage(string name)
    {
        if (!Application.isEditor) yield break;
        yield return new WaitForEndOfFrame();
        string folder = Path.Combine(Application.dataPath, "..", "Library", "CodexBridge", "Artifacts");
        Directory.CreateDirectory(folder);
        Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes(Path.Combine(folder, $"signal-watch-{name}.png"), image.EncodeToPNG()); }
        finally { UnityEngine.Object.Destroy(image); }
    }

    private static Behaviour Find(string type) => UnityEngine.Object.FindFirstObjectByType(
        Type.GetType(type + ", Assembly-CSharp")) as Behaviour;
    private static T Property<T>(object owner, string name) => (T)owner.GetType().GetProperty(name).GetValue(owner);
    private static object Field(object owner, string name) => owner.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Write(object owner, string name, object value) => owner.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name).Invoke(owner, args);
}
