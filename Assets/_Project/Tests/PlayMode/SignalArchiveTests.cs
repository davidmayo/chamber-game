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

public sealed class SignalArchiveTests : InputTestFixture
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
    public IEnumerator WalkFromTheGalleryPowerTheArchiveAndPlayAllThreePrograms()
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return Frames(3);
        player = Find("FirstPersonPlayerController");
        operations = GameObject.Find("Ground Ops Blockout").transform;
        Behaviour archive = Find("SignalArchiveController");
        Assert.That(archive, Is.Not.Null);
        Behaviour console = Field(archive, "console") as Behaviour;
        Assert.That(console, Is.Not.Null);
        Camera camera = player.GetComponentInChildren<Camera>();
        if (Find("RuntimeSceneSwitcher") == null)
            new GameObject("Signal Archive Test Pause Menu").AddComponent(Type.GetType("RuntimeSceneSwitcher, Assembly-CSharp"));
        Write(archive, "performanceSeconds", 1f);
        Write(console, "transitionSeconds", 0.05f);

        Transform region = operations.Find("Level 01 - Signal Archive");
        Assert.That(region, Is.Not.Null);
        Transform route = region.Find("Walking Route");
        Assert.That(route.childCount, Is.EqualTo(11));
        Assert.That(Property<int>(archive, "PoweredCount"), Is.Zero);
        Assert.That(Property<bool>(archive, "Ready"), Is.False);
        Assert.That(Property<int>(archive, "CompletedProgramCount"), Is.Zero);
        Assert.That(Field(archive, "relayPoints") as Transform[], Has.Length.EqualTo(3));

        // Arrange only the starting position at the existing cable gallery.
        // The shared first-person controller walks every doorway and return leg.
        CharacterController body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.position = route.GetChild(0).position;
        body.enabled = true;
        Physics.SyncTransforms();
        yield return Frames(3);
        for (int i = 1; i <= 4; i++)
            yield return WalkTo(route.GetChild(i).position);
        yield return WalkTo(route.GetChild(10).position);
        Assert.That(Property<bool>(archive, "PlayerInArea"), Is.True);
        Assert.That(player.transform.position.y, Is.EqualTo(-7.1f).Within(0.1f));

        float standingFov = camera.fieldOfView;
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => Property<bool>(console, "IsSeated"));
        yield return Tap(keyboard.spaceKey);
        yield return Frames(10);
        Assert.That(Property<bool>(archive, "IsPerforming"), Is.False,
            "The archive must reject playback until all three relay supplies are enabled.");
        Assert.That(Property<int>(archive, "CompletedProgramCount"), Is.Zero);
        yield return Tap(keyboard.escapeKey);
        yield return WaitFor(() => player.enabled);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "The first Escape must leave the bench without pausing.");
        Assert.That(camera.fieldOfView, Is.EqualTo(standingFov).Within(0.01f));
        yield return WalkTo(route.GetChild(4).position);

        for (int i = 5; i <= 9; i++)
        {
            yield return WalkTo(route.GetChild(i).position);
            if (i != 5 && i != 7 && i != 9) continue;
            int expectedCount = (i - 3) / 2;
            if (i == 5)
            {
                yield return Tap(keyboard.escapeKey);
                Assert.That(Time.timeScale, Is.Zero);
                yield return Tap(keyboard.fKey);
                Assert.That(Property<int>(archive, "PoweredCount"), Is.Zero,
                    "Pause must own F input even beside a relay supply.");
                yield return Tap(keyboard.escapeKey);
            }
            yield return Tap(keyboard.fKey);
            Assert.That(Property<int>(archive, "PoweredCount"), Is.EqualTo(expectedCount),
                $"The separate relay at route marker {i} must be reachable on foot.");
            if (i == 5)
            {
                yield return Tap(keyboard.fKey);
                Assert.That(Property<int>(archive, "PoweredCount"), Is.Zero, "A relay supply can be isolated again.");
                yield return Tap(keyboard.fKey);
                Assert.That(Property<int>(archive, "PoweredCount"), Is.EqualTo(1));
            }
        }
        Assert.That(Property<bool>(archive, "Ready"), Is.True);
        yield return WalkTo(route.GetChild(10).position);
        yield return Tap(keyboard.lKey);
        Light inspection = Field(Find("FacilityPlayerEffects"), "inspectionLight") as Light;
        Assert.That(inspection.enabled, Is.True);
        Assert.That(inspection.renderingLayerMask, Is.EqualTo(1 << 7),
            "The inspection light must follow the archive's independent lighting zone.");
        yield return Tap(keyboard.lKey);

        yield return Tap(keyboard.tabKey);
        Text notes = Find("FacilityShiftDisplay").transform.Find("Signal Watch Canvas/Field Notebook/Notebook Entries").GetComponent<Text>();
        Assert.That(notes.text, Does.Contain("SIGNAL ARCHIVE"));
        Assert.That(notes.preferredHeight, Is.LessThanOrEqualTo(notes.rectTransform.rect.height));
        Assert.That(notes.raycastTarget, Is.False);
        yield return Tap(keyboard.tabKey);
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => Property<bool>(console, "IsSeated"));
        float seatedFov = camera.fieldOfView;
        Quaternion seatedRotation = camera.transform.rotation;
        Set(mouse.delta, new Vector2(60f, -20f));
        yield return Frames(2);
        Set(mouse.delta, Vector2.zero);
        Assert.That(Quaternion.Angle(camera.transform.rotation, seatedRotation), Is.GreaterThan(1f),
            "The archive bench retains seated mouse look.");
        Set(mouse.scroll, new Vector2(0f, 120f));
        yield return Frames(10);
        Set(mouse.scroll, Vector2.zero);
        Assert.That(camera.fieldOfView, Is.LessThan(seatedFov - 1f));

        Assert.That(Property<int>(archive, "SelectedProgram"), Is.Zero);
        yield return Tap(keyboard.dKey);
        Assert.That(Property<int>(archive, "SelectedProgram"), Is.EqualTo(1));
        yield return Tap(keyboard.aKey);
        Assert.That(Property<int>(archive, "SelectedProgram"), Is.Zero);
        yield return Tap(keyboard.spaceKey);
        Assert.That(Property<bool>(archive, "IsPerforming"), Is.True,
            "One Space press starts the complete playback; the player need not hold it.");
        yield return Tap(keyboard.dKey);
        Assert.That(Property<int>(archive, "SelectedProgram"), Is.Zero, "A running program cannot be changed halfway through.");
        yield return Tap(keyboard.escapeKey);
        yield return WaitFor(() => player.enabled);
        Assert.That(Property<bool>(archive, "IsPerforming"), Is.True, "Playback continues after leaving the bench.");
        Assert.That(camera.fieldOfView, Is.EqualTo(standingFov).Within(0.01f));
        yield return Tap(keyboard.escapeKey);
        Assert.That(Time.timeScale, Is.Zero);
        float pausedProgress = Property<float>(archive, "PlaybackProgress01");
        yield return Frames(20);
        Assert.That(Property<float>(archive, "PlaybackProgress01"), Is.EqualTo(pausedProgress).Within(0.00001f),
            "The light performance must freeze with the rest of the facility when paused.");
        yield return Tap(keyboard.escapeKey);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => Property<bool>(console, "IsSeated"));
        yield return WaitFor(() => !Property<bool>(archive, "IsPerforming"));
        Assert.That(Property<int>(archive, "CompletedProgramCount"), Is.EqualTo(1));

        for (int program = 1; program <= 2; program++)
        {
            yield return Tap(keyboard.dKey);
            Assert.That(Property<int>(archive, "SelectedProgram"), Is.EqualTo(program));
            yield return Tap(keyboard.spaceKey);
            Assert.That(Property<bool>(archive, "IsPerforming"), Is.True);
            yield return Frames(20);
            yield return Screenshot("program-" + (program + 1));
            yield return WaitFor(() => !Property<bool>(archive, "IsPerforming"));
            Assert.That(Property<int>(archive, "CompletedProgramCount"), Is.EqualTo(program + 1));
        }
        // Replaying an already seen program must not count as a fourth program.
        yield return Tap(keyboard.spaceKey);
        yield return WaitFor(() => !Property<bool>(archive, "IsPerforming"));
        Assert.That(Property<int>(archive, "CompletedProgramCount"), Is.EqualTo(3));
        yield return Tap(keyboard.fKey);
        yield return WaitFor(() => player.enabled);
        Assert.That(camera.fieldOfView, Is.EqualTo(standingFov).Within(0.01f));

        yield return WalkTo(route.GetChild(4).position);
        for (int i = 3; i >= 0; i--)
            yield return WalkTo(route.GetChild(i).position);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main"));
        Assert.That(Property<bool>(archive, "PlayerInArea"), Is.False);
        Assert.That(Property<int>(archive, "CompletedProgramCount"), Is.EqualTo(3));
        Assert.That(UnityEngine.Object.FindObjectsByType(player.GetType(), FindObjectsSortMode.None), Has.Length.EqualTo(1));

        Transform highBay = operations.Find("Architecture/Hallway and High Bay Blockout/Empty High Bay");
        Assert.That(region.IsChildOf(highBay), Is.False);
        Collider sealedWall = highBay.Find("High Bay Hall Lower Wall").GetComponent<Collider>();
        foreach (float z in new[] { -2f, 3f, 7f })
        {
            Ray ray = new(operations.TransformPoint(new Vector3(6.8f, -5.4f, z)), operations.TransformDirection(Vector3.right));
            Assert.That(sealedWall.Raycast(ray, out _, 3f), Is.True,
                "The archive approach must leave the high bay's independent wall sealed.");
        }
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
        try { File.WriteAllBytes(Path.Combine(folder, "archive-" + name + ".png"), image.EncodeToPNG()); }
        finally { UnityEngine.Object.Destroy(image); }
    }
    private static Behaviour Find(string name) => UnityEngine.Object.FindFirstObjectByType(Type.GetType(name + ", Assembly-CSharp")) as Behaviour;
    private static T Property<T>(object owner, string name) => (T)owner.GetType().GetProperty(name).GetValue(owner);
    private static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Write(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
}
