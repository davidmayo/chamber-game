using System.Collections;
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class RuntimePauseMenuTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
    }

    [UnityTest]
    public IEnumerator SceneButtonsReceivePointerClicksAndLoadBothScenes()
    {
        SceneManager.LoadScene("GroundOps", LoadSceneMode.Single);
        yield return null;

        Type menuType = Type.GetType("RuntimeSceneSwitcher, Assembly-CSharp");
        Assert.That(menuType, Is.Not.Null, "Runtime pause menu type was not found.");
        Component existing = UnityEngine.Object.FindFirstObjectByType(menuType) as Component;
        if (existing != null)
        {
            UnityEngine.Object.Destroy(existing.gameObject);
            yield return null;
        }

        GameObject menuRoot = new("Pause Menu Test Root");
        UnityEngine.Object.DontDestroyOnLoad(menuRoot);
        menuRoot.AddComponent(menuType);
        yield return null;

        EventSystem eventSystem = EventSystem.current;
        Assert.That(eventSystem, Is.Not.Null, "The pause menu did not create an EventSystem.");
        InputSystemUIInputModule inputModule =
            eventSystem.GetComponent<InputSystemUIInputModule>();
        Assert.That(inputModule, Is.Not.Null, "The pause menu did not create a new-Input-System UI module.");
        Assert.That(inputModule.point?.action, Is.Not.Null, "The UI Point action is not assigned.");
        Assert.That(inputModule.leftClick?.action, Is.Not.Null, "The UI Click action is not assigned.");
        Assert.That(inputModule.point.action.enabled, Is.True, "The UI Point action is not enabled.");
        Assert.That(inputModule.leftClick.action.enabled, Is.True, "The UI Click action is not enabled.");

        Canvas menuCanvas = menuRoot.GetComponentInChildren<Canvas>(true);
        Assert.That(menuCanvas, Is.Not.Null, "The pause-menu Canvas was not created.");
        Assert.That(
            menuCanvas.GetComponent<GraphicRaycaster>(),
            Is.Not.Null,
            "The pause-menu Canvas cannot receive pointer hits without a GraphicRaycaster.");

        yield return Tap(keyboard.escapeKey);
        Button chamberButton = FindButton("Anechoic Chamber Button");
        yield return Click(chamberButton);
        yield return WaitForScene("Main");

        yield return Tap(keyboard.escapeKey);
        Button groundOpsButton = FindButton("Ground Ops Button");
        yield return Click(groundOpsButton);
        yield return WaitForScene("GroundOps");
    }

    private static Button FindButton(string name)
    {
        GameObject gameObject = GameObject.Find(name);
        Assert.That(gameObject, Is.Not.Null, $"Active pause-menu button '{name}' was not found.");
        Button button = gameObject.GetComponent<Button>();
        Assert.That(button, Is.Not.Null);
        Assert.That(button.interactable, Is.True);
        return button;
    }

    private IEnumerator Tap(ButtonControl button)
    {
        Press(button);
        yield return null;
        Release(button);
        yield return null;
    }

    private IEnumerator Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rect = button.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 center = (corners[0] + corners[2]) * 0.5f;

        bool clickInvoked = false;
        button.onClick.AddListener(() => clickInvoked = true);

        PointerEventData pointer = new(EventSystem.current) { position = center };
        List<RaycastResult> hits = new();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(
            hits.Exists(hit => hit.gameObject == button.gameObject),
            Is.True,
            $"'{button.name}' was not hit by the EventSystem at {center}; " +
            $"hits: {string.Join(", ", hits.ConvertAll(hit => hit.gameObject.name))}.");

        InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
        InputSystem.Update();
        yield return null;
        Press(mouse.leftButton);
        yield return null;
        Release(mouse.leftButton);
        yield return null;
        Assert.That(clickInvoked, Is.True, $"'{button.name}' did not receive the pointer click.");
    }

    private static IEnumerator WaitForScene(string expectedName)
    {
        const int maximumFrames = 120;
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            if (SceneManager.GetActiveScene().name == expectedName)
            {
                yield break;
            }
            yield return null;
        }

        Assert.Fail(
            $"Expected scene '{expectedName}', but active scene was " +
            $"'{SceneManager.GetActiveScene().name}'.");
    }
}
