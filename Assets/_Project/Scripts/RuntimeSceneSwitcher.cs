using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RuntimeSceneSwitcher : MonoBehaviour
{
    private const string MainSceneName = "Main";
    private const string GroundOpsSceneName = "GroundOps";

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

    private void OnGUI()
    {
        const float width = 180f;
        const float buttonHeight = 30f;
        const float padding = 8f;
        Rect panel = new(
            Screen.width - width - padding * 2f - 12f,
            12f,
            width + padding * 2f,
            buttonHeight * 2f + padding * 3f);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        string activeScene = SceneManager.GetActiveScene().name;
        GUI.enabled = activeScene != MainSceneName;
        if (GUI.Button(
                new Rect(panel.x + padding, panel.y + padding, width, buttonHeight),
                "Anechoic Chamber"))
        {
            SceneManager.LoadScene(MainSceneName);
        }

        GUI.enabled = activeScene != GroundOpsSceneName;
        if (GUI.Button(
                new Rect(
                    panel.x + padding,
                    panel.y + padding * 2f + buttonHeight,
                    width,
                    buttonHeight),
                "Ground Ops"))
        {
            SceneManager.LoadScene(GroundOpsSceneName);
        }

        GUI.enabled = true;
        GUI.color = previousColor;
    }
}
