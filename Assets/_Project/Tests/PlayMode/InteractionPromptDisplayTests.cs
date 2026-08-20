using System.Collections;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class InteractionPromptDisplayTests
{
    [UnityTest]
    public IEnumerator PromptUsesResolutionScaledCanvasAndCanBeHidden()
    {
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

        hide.Invoke(null, new object[] { owner });
        yield return null;
        Assert.That(text.gameObject.activeInHierarchy, Is.False);

        UnityEngine.Object.Destroy(owner);
    }
}
