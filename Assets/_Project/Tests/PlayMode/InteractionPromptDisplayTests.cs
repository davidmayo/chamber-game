using System.Collections;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public sealed class InteractionPromptDisplayTests
{
    [UnityTest]
    public IEnumerator PromptUsesResolutionScaledCanvasAndCanBeHidden()
    {
        // This UI test must not inherit proximity prompts from the last gameplay
        // test's player position.
        Scene previousScene = SceneManager.GetActiveScene();
        Scene emptyScene = SceneManager.CreateScene("Interaction Prompt Test");
        SceneManager.SetActiveScene(emptyScene);
        yield return SceneManager.UnloadSceneAsync(previousScene);

        GameObject owner = new("Interaction Prompt Test Owner");
        const string message = "Press F to test prompt";

        Type displayType = Type.GetType("InteractionPromptDisplay, Assembly-CSharp");
        Assert.That(displayType, Is.Not.Null);
        MethodInfo show = displayType.GetMethod(
            "Show",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo hide = displayType.GetMethod(
            "Hide",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(show, Is.Not.Null);
        Assert.That(hide, Is.Not.Null);

        show.Invoke(null, new object[] { owner, message });
        yield return null;

        Component display = UnityEngine.Object.FindFirstObjectByType(displayType) as Component;
        Assert.That(display, Is.Not.Null);

        Canvas canvas = display.GetComponentInChildren<Canvas>(true);
        Assert.That(canvas, Is.Not.Null);
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Assert.That(scaler, Is.Not.Null);
        Assert.That(
            scaler.uiScaleMode,
            Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));

        Text text = canvas.GetComponentInChildren<Text>(true);
        Assert.That(text, Is.Not.Null);
        Assert.That(text.gameObject.activeInHierarchy, Is.True);
        Assert.That(text.text, Is.EqualTo(message));
        Assert.That(text.fontSize, Is.EqualTo(22));
        foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
        {
            Assert.That(graphic.raycastTarget, Is.False,
                "Display-only hints must not intercept pause-menu clicks.");
        }

        const string longMessage = "A / D azimuth   W / S elevation   Shift: fine   Ctrl: fast\n"
            + "Mouse: look   Wheel: zoom   F / Esc: stand up";
        show.Invoke(null, new object[] { owner, longMessage });
        yield return null;
        Canvas.ForceUpdateCanvases();
        RectTransform panel = text.transform.parent.GetComponent<RectTransform>();
        Assert.That(panel.rect.height, Is.GreaterThan(58f),
            "Multiple lines need a taller background.");
        Assert.That(text.rectTransform.rect.height, Is.GreaterThanOrEqualTo(text.preferredHeight));

        // Simulate a narrow logical viewport without resizing the user's Editor.
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = Screen.width / 420f;
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.That(panel.rect.width, Is.LessThanOrEqualTo(340.1f));
        Assert.That(text.rectTransform.rect.height, Is.GreaterThanOrEqualTo(text.preferredHeight));
        Assert.That(text.text, Is.EqualTo(longMessage));

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        hide.Invoke(null, new object[] { owner });
        yield return null;
        Assert.That(text.gameObject.activeInHierarchy, Is.False);

        UnityEngine.Object.Destroy(owner);
    }
}
