using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameControlModeController : MonoBehaviour
{
    private enum ControlMode
    {
        Player,
        Table,
    }

    [SerializeField] private FirstPersonPlayerController playerController;
    [SerializeField] private TurntableController tableController;
    [SerializeField] private ControlMode controlMode = ControlMode.Player;

    public void Configure(
        FirstPersonPlayerController player,
        TurntableController table)
    {
        playerController = player;
        tableController = table;
        ApplyMode();
    }

    private void Awake()
    {
        ApplyMode();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.tabKey.wasPressedThisFrame)
        {
            SetMode(controlMode == ControlMode.Player
                ? ControlMode.Table
                : ControlMode.Player);
        }
        else if (keyboard.escapeKey.wasPressedThisFrame && controlMode == ControlMode.Player)
        {
            SetCursorCaptured(false);
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            SetCursorCaptured(false);
        }
    }

    private void SetMode(ControlMode mode)
    {
        controlMode = mode;
        ApplyMode();
    }

    private void ApplyMode()
    {
        bool controllingPlayer = controlMode == ControlMode.Player;
        if (playerController != null)
        {
            playerController.enabled = controllingPlayer;
        }
        if (tableController != null)
        {
            tableController.enabled = !controllingPlayer;
        }

        if (Application.isPlaying)
        {
            SetCursorCaptured(controllingPlayer);
        }
    }

    private static void SetCursorCaptured(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    private void OnGUI()
    {
        Rect panel = new(Screen.width - 246f, 16f, 230f, 104f);
        Color previousColor = GUI.color;
        GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, panel.height - 16f));
        GUILayout.Label("CONTROL MODE", EditorLikeTitleStyle());
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(controlMode == ControlMode.Player ? "PLAYER *" : "PLAYER"))
            SetMode(ControlMode.Player);
        if (GUILayout.Button(controlMode == ControlMode.Table ? "TABLE *" : "TABLE"))
            SetMode(ControlMode.Table);
        GUILayout.EndHorizontal();
        GUILayout.Label(
            controlMode == ControlMode.Player
                ? "WASD MOVE  •  MOUSE LOOK  •  ESC RELEASE"
                : "A/D PAN  •  W/S TILT  •  Q/E HEIGHT",
            EditorLikeHintStyle());
        GUILayout.Label("TAB SWITCHES MODE", EditorLikeHintStyle());
        GUILayout.EndArea();

        GUI.color = previousColor;
    }

    private static GUIStyle EditorLikeTitleStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.45f, 0.82f, 1f) },
        };
    }

    private static GUIStyle EditorLikeHintStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 9,
            normal = { textColor = new Color(0.72f, 0.76f, 0.82f) },
        };
    }
}
