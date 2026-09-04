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

public sealed class FacilityInteractionTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;
    private Behaviour player;
    private Camera camera;

    private static readonly string[] ConsoleTypes =
    {
        "ComputerConsoleController",
        "GroundOpsDishConsoleController",
        "SimpleSeatedConsoleController",
    };

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
    }

    public override void TearDown()
    {
        // Even a failed assertion must leave the next test able to move.
        Behaviour menu = Find("RuntimeSceneSwitcher");
        if (menu != null) UnityEngine.Object.DestroyImmediate(menu.gameObject);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        base.TearDown();
    }

    [UnityTest]
    public IEnumerator PauseBlocksEveryNearbyEquipmentInteraction()
    {
        yield return LoadFacility();
        foreach (string type in new[]
                 {
                     "ComputerConsoleController", "GroundOpsDishConsoleController",
                     "SimpleSeatedConsoleController", "ScissorLiftStationController",
                     "FloodLightController",
                 })
        {
            Behaviour station = Find(type);
            Assert.That(station, Is.Not.Null, type);
            yield return Approach(station);
            Vector3 standingPosition = camera.transform.position;
            Quaternion standingRotation = camera.transform.rotation;
            Behaviour flood = Find("FloodLightController");
            bool lightsWereOn = (bool)Read(flood, "lightsOn");

            yield return Tap(keyboard.escapeKey);
            Assert.That(Time.timeScale, Is.Zero, "Escape must open the pause menu.");
            if (type == "ComputerConsoleController")
            {
                yield return CaptureForReview("pause-menu");
            }
            yield return Tap(keyboard.fKey);
            Press(keyboard.wKey);
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(90f, 45f));
            yield return null;
            Release(keyboard.wKey);
            yield return null;

            Assert.That(player.enabled, Is.True, $"{type} accepted F while paused.");
            Assert.That(Vector3.Distance(camera.transform.position, standingPosition),
                Is.LessThan(0.001f), $"{type} moved the camera while paused.");
            Assert.That(Quaternion.Angle(camera.transform.rotation, standingRotation),
                Is.LessThan(0.01f));
            Assert.That((bool)Read(flood, "lightsOn"), Is.EqualTo(lightsWereOn));
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
            Assert.That(Prompt().gameObject.activeInHierarchy, Is.False);
            yield return Tap(keyboard.escapeKey);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }

    [UnityTest]
    public IEnumerator EveryConsoleSupportsBothWheelFormatsAndRestoresStandingView()
    {
        yield return LoadFacility();
        foreach (string type in ConsoleTypes)
        {
            Behaviour station = Find(type);
            yield return Approach(station);
            Write(station, "transitionSeconds", 0.01f);
            Write(station, "zoomSmoothing", 1000f);
            Vector3 standingPosition = camera.transform.localPosition;
            Quaternion standingRotation = camera.transform.localRotation;
            float standingFov = camera.fieldOfView;

            yield return Tap(keyboard.fKey);
            yield return WaitForState(station, "Seated");
            Assert.That(player.enabled, Is.False, type);
            Assert.That(Prompt().text, Does.Contain("Wheel: zoom"));
            Assert.That(Prompt().text, Does.Contain("stand up"));
            yield return CaptureForReview(type);
            float initialFov = camera.fieldOfView;
            yield return Scroll(1f);
            float normalizedChange = initialFov - camera.fieldOfView;
            Assert.That(normalizedChange, Is.InRange(4f, 7f), type);
            yield return Scroll(-1f);
            Assert.That(camera.fieldOfView, Is.EqualTo(initialFov).Within(0.05f));
            yield return Scroll(120f);
            Assert.That(initialFov - camera.fieldOfView,
                Is.EqualTo(normalizedChange).Within(0.05f),
                $"{type} zoom depends on the device's wheel format.");
            yield return Scroll(12000f);
            Assert.That(camera.fieldOfView, Is.EqualTo(25f).Within(0.05f));

            // Disabling a different, unused console must not steal this seat.
            foreach (string otherType in ConsoleTypes)
            {
                if (otherType == type) continue;
                Behaviour other = Find(otherType);
                other.enabled = false;
                yield return null;
                Assert.That(player.enabled, Is.False,
                    $"Disabling {otherType} interrupted {type}.");
                other.enabled = true;
            }

            yield return Tap(keyboard.escapeKey);
            yield return WaitForState(station, "Standing");
            Assert.That(Time.timeScale, Is.EqualTo(1f),
                "The first Escape must leave the console without opening pause.");
            AssertStandingView(standingPosition, standingRotation, standingFov);

            // Re-entering after exit must work, and disabling the active console
            // must safely restore the standing view even while zoomed in.
            yield return Tap(keyboard.fKey);
            yield return WaitForState(station, "Seated");
            yield return Scroll(120f);
            station.enabled = false;
            yield return null;
            AssertStandingView(standingPosition, standingRotation, standingFov);
            station.enabled = true;
        }
    }

    [UnityTest]
    public IEnumerator ChamberConsoleLeavesHeightToTheRearWallControl()
    {
        yield return LoadFacility();
        Behaviour console = Find("ComputerConsoleController");
        Behaviour table = Find("TurntableController");
        Behaviour source = Find("SourceAntennaController");
        yield return Approach(console);
        Write(console, "transitionSeconds", 0.01f);
        yield return Tap(keyboard.fKey);
        yield return WaitForState(console, "Seated");
        float height = (float)Read(table, "heightMeters");
        float polarity = (float)Read(source, "polarityDegrees");
        Assert.That(Prompt().text.ToLowerInvariant(), Does.Not.Contain("height"));
        Assert.That(Prompt().text, Does.Contain("polarity"));
        Press(keyboard.eKey);
        yield return new WaitForSeconds(0.1f);
        Release(keyboard.eKey);
        yield return null;
        Assert.That((float)Read(source, "polarityDegrees"), Is.Not.EqualTo(polarity));
        Assert.That((float)Read(table, "heightMeters"), Is.EqualTo(height));
        yield return Tap(keyboard.fKey);
        yield return WaitForState(console, "Standing");

        Behaviour lift = Find("ScissorLiftStationController");
        yield return Approach(lift);
        yield return Tap(keyboard.fKey);
        Assert.That(player.enabled, Is.False);
        Assert.That(Prompt().text, Does.Contain("lower"));
        Press(keyboard.eKey);
        yield return new WaitForSeconds(0.1f);
        Release(keyboard.eKey);
        yield return null;
        Assert.That((float)Read(table, "heightMeters"), Is.GreaterThan(height));
        yield return Tap(keyboard.escapeKey);
        Assert.That(player.enabled, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    private IEnumerator LoadFacility()
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return null;
        player = Find("FirstPersonPlayerController");
        camera = Camera.main;
        Assert.That(player, Is.Not.Null);
        Assert.That(camera, Is.Not.Null);
        if (Find("RuntimeSceneSwitcher") == null)
        {
            new GameObject("Interaction Test Pause Menu").AddComponent(
                Type.GetType("RuntimeSceneSwitcher, Assembly-CSharp"));
        }
        yield return null;
    }

    private IEnumerator Approach(Behaviour station)
    {
        Assert.That(station, Is.Not.Null);
        // Keep the collider enabled so leaving the previous station delivers
        // OnTriggerExit before entering the next station.
        Vector3 position = station.GetComponent<BoxCollider>().bounds.center;
        position.y = 0f;
        player.transform.position = position;
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That((bool)Read(station, "playerNearby"), Is.True,
            $"Player did not enter {station.GetType().Name}'s trigger.");
    }

    private void AssertStandingView(Vector3 position, Quaternion rotation, float fov)
    {
        Assert.That(player.enabled, Is.True);
        Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(camera.transform.localRotation, rotation), Is.LessThan(0.01f));
        Assert.That(camera.fieldOfView, Is.EqualTo(fov).Within(0.01f));
    }

    private IEnumerator Tap(ButtonControl button)
    {
        Press(button);
        yield return null;
        Release(button);
        yield return null;
    }

    private IEnumerator Scroll(float value)
    {
        InputSystem.QueueDeltaStateEvent(mouse.scroll, new Vector2(0f, value));
        yield return null;
        yield return new WaitForSecondsRealtime(0.05f);
    }

    private static IEnumerator CaptureForReview(string name)
    {
        if (!Application.isEditor) yield break;
        yield return new WaitForEndOfFrame();
        // Include screen-space UI in these review images; camera.Render alone
        // omits the prompt and pause canvases.
        string folder = Path.Combine(Application.dataPath, "..", "Library", "CodexBridge", "Artifacts");
        Directory.CreateDirectory(folder);
        int supersize = Mathf.Clamp(Mathf.CeilToInt(1280f / Screen.width), 1, 4);
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture(supersize);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, $"review-{name}.png"), screenshot.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.Destroy(screenshot);
        }
    }

    private static IEnumerator WaitForState(Behaviour station, string state)
    {
        float deadline = Time.realtimeSinceStartup + 2f;
        while (Read(station, "state").ToString() != state && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        yield return null;
        Assert.That(Read(station, "state").ToString(), Is.EqualTo(state),
            $"{station.GetType().Name} did not reach {state}; nearby={Read(station, "playerNearby")}.");
    }

    private static Text Prompt() => Find("InteractionPromptDisplay").GetComponentInChildren<Text>(true);

    private static Behaviour Find(string type)
    {
        Type componentType = Type.GetType(type + ", Assembly-CSharp");
        if (type == "SimpleSeatedConsoleController")
            return GameObject.Find("Ground Ops Blockout/Server Room Equipment/DSN Server Rack")
                .GetComponentInChildren(componentType) as Behaviour;
        return UnityEngine.Object.FindFirstObjectByType(componentType) as Behaviour;
    }

    private static object Read(object target, string field) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void Write(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
