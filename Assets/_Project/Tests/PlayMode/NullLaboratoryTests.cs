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

public sealed class NullLaboratoryTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;
    private Behaviour player;
    private Transform operations;
    private float previousCaptureDelta;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
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
    public IEnumerator WalkDownOperateTheNullBenchAndWalkBackToTheChamber()
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return Frames(3);
        player = Find("FirstPersonPlayerController");
        operations = GameObject.Find("Ground Ops Blockout").transform;
        Behaviour lab = Find("NullLaboratoryController");
        Behaviour console = Field(lab, "console") as Behaviour;
        Camera camera = player.GetComponentInChildren<Camera>();
        if (Find("RuntimeSceneSwitcher") == null)
            new GameObject("Null Lab Test Pause Menu").AddComponent(Type.GetType("RuntimeSceneSwitcher, Assembly-CSharp"));
        Assert.That(lab, Is.Not.Null);
        Assert.That(Property<bool>(lab, "Powered"), Is.False);
        Write(lab, "captureSeconds", 0.4f);
        Write(console, "transitionSeconds", 0.05f);
        Transform route = lab.transform.Find("Walking Route");
        // Only arrange the starting position. Every stair, landing, doorway,
        // and return leg below is traversed by the real standing controller.
        CharacterController body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.position = route.GetChild(0).position;
        body.enabled = true;
        Physics.SyncTransforms();
        yield return Frames(3);
        for (int i = 1; i < route.childCount; i++)
        {
            yield return WalkTo(route.GetChild(i).position);
            if (i == 2) yield return Screenshot("stair-entry");
            if (i == 6) yield return Screenshot("cable-gallery");
            if (i == 7)
            {
                yield return Tap(keyboard.escapeKey);
                yield return Tap(keyboard.fKey);
                Assert.That(Property<bool>(lab, "Powered"), Is.False, "Pause owns the supply switch input.");
                yield return Tap(keyboard.escapeKey);
                yield return Tap(keyboard.fKey);
                Assert.That(Property<bool>(lab, "Powered"), Is.True);
            }
        }
        Assert.That(player.transform.position.y, Is.EqualTo(-7.1f).Within(0.1f));
        Assert.That(Property<bool>(lab, "PlayerInArea"), Is.True);
        yield return Tap(keyboard.lKey);
        Light inspection = Field(Find("FacilityPlayerEffects"), "inspectionLight") as Light;
        Assert.That(inspection.renderingLayerMask, Is.EqualTo(1 << 6));
        yield return Tap(keyboard.lKey);
        player.transform.rotation = operations.rotation;
        yield return Screenshot("lab-arrival");
        float standingFov = camera.fieldOfView;
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => Property<bool>(console, "IsSeated"));
        float seatedFov = camera.fieldOfView;
        Set(mouse.scroll, new Vector2(0f, 120f));
        yield return Frames(10);
        Assert.That(camera.fieldOfView, Is.LessThan(seatedFov - 1f));
        Set(mouse.scroll, Vector2.zero);
        Press(keyboard.spaceKey);
        yield return Frames(35);
        Release(keyboard.spaceKey);
        yield return Frames(2);
        Assert.That(keyboard.spaceKey.isPressed, Is.False);
        Assert.That(Property<bool>(lab, "Certified"), Is.False, "An unbalanced signal must be rejected.");
        yield return Tune(lab, "PhaseDegrees", -125f, keyboard.aKey, keyboard.dKey, 0.3f);
        yield return Tune(lab, "Amplitude", 0.65f, keyboard.sKey, keyboard.wKey, 0.004f);
        Assert.That(Property<bool>(lab, "Balanced"), Is.True);
        yield return Screenshot("balanced-waveforms");
        // Releasing Space must discard a partial measurement.
        Press(keyboard.spaceKey);
        yield return Frames(5);
        Release(keyboard.spaceKey);
        yield return Frames(2);
        Assert.That(Property<float>(lab, "CaptureProgress01"), Is.Zero);
        Press(keyboard.spaceKey);
        yield return WaitFor(() => Property<bool>(lab, "Certified"));
        Release(keyboard.spaceKey);
        Assert.That(Property<float>(lab, "Residual"), Is.LessThanOrEqualTo(0.025f));
        yield return Frames(2);
        yield return Tap(keyboard.rKey);
        Assert.That(Property<bool>(lab, "Certified"), Is.False, "R must start a fresh balance at the bench.");
        yield return Tune(lab, "PhaseDegrees", -125f, keyboard.aKey, keyboard.dKey, 0.3f);
        yield return Tune(lab, "Amplitude", 0.65f, keyboard.sKey, keyboard.wKey, 0.004f);
        Press(keyboard.spaceKey);
        yield return WaitFor(() => Property<bool>(lab, "Certified"));
        Release(keyboard.spaceKey);
        yield return Frames(2);
        yield return Tap(keyboard.tabKey);
        Text notes = Find("FacilityShiftDisplay").transform.Find("Signal Watch Canvas/Field Notebook/Notebook Entries").GetComponent<Text>();
        Assert.That(notes.text, Does.Contain("NULL REFERENCE LABORATORY"));
        Assert.That(notes.preferredHeight, Is.LessThanOrEqualTo(notes.rectTransform.rect.height));
        yield return Screenshot("field-notes");
        yield return Tap(keyboard.tabKey);
        yield return Tap(keyboard.escapeKey);
        yield return WaitFor(() => player.enabled);
        Assert.That(camera.fieldOfView, Is.EqualTo(standingFov).Within(0.01f));
        // Enter the test cell through its own opening and inspect the apparatus.
        yield return WalkTo(operations.TransformPoint(new Vector3(2.4f, -7.1f, 16.0f)));
        yield return WalkTo(operations.TransformPoint(new Vector3(2.4f, -7.1f, 21.2f)));
        player.transform.rotation = Quaternion.LookRotation(operations.TransformDirection(Vector3.left));
        yield return Screenshot("null-cell-certified");
        yield return WalkTo(operations.TransformPoint(new Vector3(2.4f, -7.1f, 16.0f)));
        yield return WalkTo(route.GetChild(9).position);
        for (int i = 8; i >= 0; i--)
        {
            if (i == 7) continue; // The supply cabinet is a deliberate side visit.
            yield return WalkTo(route.GetChild(i).position);
        }
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main"));
        Assert.That(player.transform.position.y, Is.EqualTo(0f).Within(0.1f));
        Assert.That(Property<bool>(lab, "Certified"), Is.True);
        Assert.That(Property<bool>(lab, "PlayerInArea"), Is.False);
        Assert.That((Field(lab, "tone") as AudioSource).volume, Is.Zero, "The lab tone must not follow the player upstairs.");
        Transform highBay = operations.Find("Architecture/Hallway and High Bay Blockout/Empty High Bay");
        Assert.That(lab.transform.IsChildOf(highBay), Is.False);
        Bounds labFloor = lab.transform.Find("Rooms/Lab Floor").GetComponent<Renderer>().bounds;
        Assert.That(labFloor.max.y, Is.EqualTo(-7.1f).Within(0.01f));
        Assert.That(labFloor.min.x, Is.LessThan(0f));
        Assert.That(labFloor.max.x, Is.GreaterThan(0f));
        Assert.That(labFloor.min.z, Is.LessThan(0f));
        Assert.That(labFloor.max.z, Is.GreaterThan(0f), "The new lab floor must lie beneath the chamber's world origin.");
        Collider sealedWall = highBay.Find("High Bay Hall Lower Wall").GetComponent<Collider>();
        foreach (float z in new[] { 13f, 19f, 25f })
        {
            Ray ray = new(operations.TransformPoint(new Vector3(6.8f, -5.4f, z)), operations.TransformDirection(Vector3.right));
            Assert.That(sealedWall.Raycast(ray, out _, 3f), Is.True, "The high bay must remain independently sealed.");
        }
        foreach (Renderer renderer in lab.GetComponentsInChildren<Renderer>())
            Assert.That(renderer.renderingLayerMask, Is.EqualTo(1u << 6));
        Assert.That(UnityEngine.Object.FindObjectsByType(player.GetType(), FindObjectsSortMode.None), Has.Length.EqualTo(1));
    }

    private IEnumerator WalkTo(Vector3 destination)
    {
        int frames = 0;
        while (Vector3.ProjectOnPlane(destination - player.transform.position, Vector3.up).magnitude > 0.12f && frames++ < 900)
        {
            Vector3 direction = Vector3.ProjectOnPlane(destination - player.transform.position, Vector3.up);
            player.transform.rotation = Quaternion.LookRotation(direction);
            Press(keyboard.wKey);
            yield return null;
            Assert.That(player.transform.position.y, Is.GreaterThan(-7.3f), "The player fell below the first floor.");
        }
        Release(keyboard.wKey);
        yield return Frames(5);
        Assert.That(Vector3.Distance(player.transform.position, destination), Is.LessThan(0.3f),
            $"Route blocked at local {operations.InverseTransformPoint(player.transform.position)} on the way to {operations.InverseTransformPoint(destination)}.");
        Assert.That(Physics.Raycast(player.transform.position + Vector3.up * 0.3f, Vector3.down, out _, 0.7f,
            ~0, QueryTriggerInteraction.Ignore), Is.True, "There must be physical floor support along the route.");
    }

    private IEnumerator Tune(Behaviour lab, string property, float target, ButtonControl negative, ButtonControl positive, float tolerance)
    {
        int frames = 0;
        while (Mathf.Abs(Property<float>(lab, property) - target) > tolerance && frames++ < 900)
        {
            Press(keyboard.leftShiftKey);
            if (Mathf.Abs(Property<float>(lab, property) - target) > tolerance * 12f) Release(keyboard.leftShiftKey);
            ButtonControl key = Property<float>(lab, property) < target ? positive : negative;
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }
        Release(keyboard.leftShiftKey);
        Assert.That(Property<float>(lab, property), Is.EqualTo(target).Within(tolerance),
            $"{property}: certified={Property<bool>(lab, "Certified")}; seated={Property<bool>(Field(lab, "console"), "IsSeated")}; space={keyboard.spaceKey.isPressed}; timeScale={Time.timeScale}");
    }

    private IEnumerator Tap(ButtonControl key) { Press(key); yield return null; Release(key); yield return null; }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator WaitFor(Func<bool> condition)
    {
        int frames = 0;
        while (!condition() && frames++ < 300) yield return null;
        Assert.That(condition(), Is.True, "Expected interaction state was not reached.");
        yield return null;
    }
    private static IEnumerator Screenshot(string name)
    {
        yield return new WaitForEndOfFrame();
        string folder = Path.Combine(Application.dataPath, "..", "Library", "CodexBridge", "Artifacts");
        Directory.CreateDirectory(folder);
        Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes(Path.Combine(folder, "null-lab-" + name + ".png"), image.EncodeToPNG()); }
        finally { UnityEngine.Object.Destroy(image); }
    }
    private static Behaviour Find(string name) => UnityEngine.Object.FindFirstObjectByType(Type.GetType(name + ", Assembly-CSharp")) as Behaviour;
    private static T Property<T>(object owner, string name) => (T)owner.GetType().GetProperty(name).GetValue(owner);
    private static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Write(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
}
