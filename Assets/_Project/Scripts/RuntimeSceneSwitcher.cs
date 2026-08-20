using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class RuntimeSceneSwitcher : MonoBehaviour
{
    private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
    private const string GroundOpsScenePath = "Assets/_Project/Scenes/GroundOps.unity";

    private bool menuOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (FindFirstObjectByType<RuntimeSceneSwitcher>() != null)
        {
            return;
        }

        GameObject gameObject = new("Runtime Scene Switcher");
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<RuntimeSceneSwitcher>();
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

    private void OnGUI()
    {
        if (!menuOpen)
        {
            return;
        }

        const float width = 280f;
        const float buttonHeight = 42f;
        const float padding = 16f;
        const float titleHeight = 42f;
        const float gap = 10f;
        float panelHeight = padding * 2f + titleHeight + buttonHeight * 3f + gap * 2f;
        Rect panel = new(
            (Screen.width - width - padding * 2f) / 2f,
            (Screen.height - panelHeight) / 2f,
            width + padding * 2f,
            panelHeight);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle titleStyle = new(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
        };
        GUI.Label(
            new Rect(panel.x + padding, panel.y + padding, width, titleHeight),
            "Paused",
            titleStyle);

        float buttonY = panel.y + padding + titleHeight;
        if (GUI.Button(new Rect(panel.x + padding, buttonY, width, buttonHeight), "Resume"))
        {
            CloseMenu();
            return;
        }

        buttonY += buttonHeight + gap;
        int activeBuildIndex = SceneManager.GetActiveScene().buildIndex;
        int mainBuildIndex = SceneUtility.GetBuildIndexByScenePath(MainScenePath);
        GUI.enabled = activeBuildIndex != mainBuildIndex;
        if (GUI.Button(new Rect(panel.x + padding, buttonY, width, buttonHeight), "Anechoic Chamber"))
        {
            LoadScene(MainScenePath);
            return;
        }

        buttonY += buttonHeight + gap;
        int groundOpsBuildIndex = SceneUtility.GetBuildIndexByScenePath(GroundOpsScenePath);
        GUI.enabled = activeBuildIndex != groundOpsBuildIndex;
        if (GUI.Button(new Rect(panel.x + padding, buttonY, width, buttonHeight), "Ground Ops"))
        {
            LoadScene(GroundOpsScenePath);
            return;
        }

        GUI.enabled = true;
        GUI.color = previousColor;
    }

    private void OpenMenu()
    {
        menuOpen = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        SetCursorCaptured(false);
    }

    private void CloseMenu()
    {
        menuOpen = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SetCursorCaptured(true);
    }

    private void LoadScene(string scenePath)
    {
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
        if (buildIndex < 0)
        {
            Debug.LogError($"Pause menu scene is not enabled in Build Profiles: {scenePath}", this);
            return;
        }

        menuOpen = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
    }

    private void OnDisable()
    {
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
