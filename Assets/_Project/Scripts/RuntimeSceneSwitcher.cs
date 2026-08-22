using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
public sealed class RuntimeSceneSwitcher : MonoBehaviour
{
    private GameObject menuCanvas;
    private Button resumeButton;
    private InputSystemUIInputModule uiInputModule;
    private bool menuOpen;

    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (FindFirstObjectByType<RuntimeSceneSwitcher>() != null)
        {
            return;
        }

        GameObject gameObject = new("Runtime Pause Menu");
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<RuntimeSceneSwitcher>();
    }

    private void Awake()
    {
        EnsureUiEventSystem();
        CreateMenuCanvas();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (menuOpen)
        {
            CloseMenu();
            return;
        }

        // Console and lift modes disable the player controller and retain their
        // established first-Escape behavior of standing up or leaving the control.
        FirstPersonPlayerController player = FindFirstObjectByType<FirstPersonPlayerController>();
        if (player != null && !player.enabled)
        {
            return;
        }

        OpenMenu();
    }

    private void EnsureUiEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new(
                "Pause Menu Event System",
                typeof(EventSystem));
            eventSystemObject.transform.SetParent(transform, false);
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        StandaloneInputModule legacyModule =
            eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
        {
            legacyModule.enabled = false;
        }

        uiInputModule =
            eventSystem.GetComponent<InputSystemUIInputModule>();
        if (uiInputModule == null)
        {
            uiInputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        // Runtime-created input modules can retain empty/stale action references
        // when Enter Play Mode Options skip a domain reload. Explicitly assign the
        // package's standard Point/Click/Navigate actions every time this menu is
        // created instead of relying on AddComponent's OnEnable side effect.
        uiInputModule.AssignDefaultActions();
    }

    private void CreateMenuCanvas()
    {
        menuCanvas = new GameObject(
            "Pause Menu Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        menuCanvas.transform.SetParent(transform, false);

        Canvas canvas = menuCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = menuCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = CreateUiObject("Panel", menuCanvas.transform);
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(360f, 180f);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.025f, 0.035f, 0.05f, 0.94f);

        VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 22, 22);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Text title = CreateText("Title", panel, "Paused", 30, FontStyle.Bold);
        LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 56f;

        resumeButton = CreateButton("Resume Button", panel, "Resume");
        resumeButton.onClick.AddListener(CloseMenu);

        menuCanvas.SetActive(false);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string content,
        int fontSize,
        FontStyle fontStyle = FontStyle.Normal)
    {
        GameObject textObject = CreateUiObject(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.20f, 0.27f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(0.72f, 0.78f, 0.88f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
        button.colors = colors;

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 58f;

        Text text = CreateText("Label", buttonObject.transform, label, 22);
        RectTransform textTransform = text.rectTransform;
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;
        return button;
    }

    private void OpenMenu()
    {
        menuOpen = true;
        IsOpen = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        menuCanvas.SetActive(true);
        SetCursorCaptured(false);
    }

    private void CloseMenu()
    {
        menuOpen = false;
        IsOpen = false;
        menuCanvas.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SetCursorCaptured(true);
    }

    private void OnDisable()
    {
        IsOpen = false;
        if (!menuOpen)
        {
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private static void SetCursorCaptured(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }
}
