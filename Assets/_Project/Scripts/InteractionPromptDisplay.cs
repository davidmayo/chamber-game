using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public sealed class InteractionPromptDisplay : MonoBehaviour
{
    private static InteractionPromptDisplay instance;

    private readonly Dictionary<Object, string> prompts = new();
    private Object activeOwner;
    private GameObject panel;
    private Text label;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (instance != null)
        {
            return;
        }

        InteractionPromptDisplay existing =
            FindFirstObjectByType<InteractionPromptDisplay>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject root = new("Interaction Prompt Display");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<InteractionPromptDisplay>();
    }

    public static void Show(Object owner, string message)
    {
        if (owner == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        EnsureExists();
        instance.prompts[owner] = message;
        if (instance.activeOwner == null)
        {
            instance.activeOwner = owner;
        }
    }

    public static void Hide(Object owner)
    {
        if (instance == null || owner == null)
        {
            return;
        }

        instance.prompts.Remove(owner);
        if (instance.activeOwner == owner)
        {
            instance.activeOwner = null;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        CreateCanvas();
    }

    private void LateUpdate()
    {
        RemoveDestroyedOwners();
        SelectActiveOwner();

        string message = null;
        bool shouldShow = activeOwner != null
            && prompts.TryGetValue(activeOwner, out message)
            && !RuntimeSceneSwitcher.IsOpen;
        panel.SetActive(shouldShow);
        if (shouldShow)
        {
            label.text = message;
        }
    }

    private void CreateCanvas()
    {
        GameObject canvasObject = new(
            "Interaction Prompt Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        panel = new GameObject(
            "Prompt Panel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelTransform = panel.GetComponent<RectTransform>();
        panelTransform.anchorMin = new Vector2(0.5f, 0f);
        panelTransform.anchorMax = new Vector2(0.5f, 0f);
        panelTransform.pivot = new Vector2(0.5f, 0f);
        panelTransform.anchoredPosition = new Vector2(0f, 40f);
        panelTransform.sizeDelta = new Vector2(520f, 58f);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.025f, 0.035f, 0.05f, 0.9f);
        background.raycastTarget = false;

        GameObject labelObject = new(
            "Prompt Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        labelObject.transform.SetParent(panel.transform, false);

        RectTransform labelTransform = labelObject.GetComponent<RectTransform>();
        labelTransform.anchorMin = Vector2.zero;
        labelTransform.anchorMax = Vector2.one;
        labelTransform.offsetMin = new Vector2(18f, 8f);
        labelTransform.offsetMax = new Vector2(-18f, -8f);

        label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        panel.SetActive(false);
    }

    private void RemoveDestroyedOwners()
    {
        List<Object> destroyed = null;
        foreach (Object owner in prompts.Keys)
        {
            if (owner != null)
            {
                continue;
            }

            destroyed ??= new List<Object>();
            destroyed.Add(owner);
        }

        if (destroyed == null)
        {
            return;
        }

        foreach (Object owner in destroyed)
        {
            prompts.Remove(owner);
        }
    }

    private void SelectActiveOwner()
    {
        if (activeOwner != null && prompts.ContainsKey(activeOwner))
        {
            return;
        }

        activeOwner = null;
        foreach (Object owner in prompts.Keys)
        {
            if (owner != null)
            {
                activeOwner = owner;
                break;
            }
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
