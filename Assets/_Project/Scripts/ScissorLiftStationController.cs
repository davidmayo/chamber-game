using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class ScissorLiftStationController : MonoBehaviour
{
    [SerializeField] private FirstPersonPlayerController playerController;
    [SerializeField] private TurntableController turntableController;

    private bool playerNearby;
    private bool controllingLift;

    public void Configure(
        FirstPersonPlayerController player,
        TurntableController turntable)
    {
        playerController = player;
        turntableController = turntable;
        turntableController.enabled = false;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (!controllingLift)
        {
            if (playerNearby && keyboard.fKey.wasPressedThisFrame)
            {
                controllingLift = true;
                playerController.enabled = false;
                turntableController.enabled = false;
            }
            return;
        }

        if (keyboard.fKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
        {
            controllingLift = false;
            playerController.enabled = true;
            return;
        }

        float heightInput = ButtonAxis(keyboard.qKey.isPressed, keyboard.eKey.isPressed);
        turntableController.ApplyInput(0f, 0f, heightInput, Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<FirstPersonPlayerController>() == playerController)
        {
            playerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<FirstPersonPlayerController>() == playerController)
        {
            playerNearby = false;
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying && controllingLift && playerController != null)
        {
            playerController.enabled = true;
        }
        controllingLift = false;
    }

    private void OnGUI()
    {
        if (controllingLift)
        {
            DrawPrompt("Q lower / E raise     F or ESC to stop");
        }
        else if (playerNearby)
        {
            DrawPrompt("Press F to use scissor lift");
        }
    }

    private static float ButtonAxis(bool negativePressed, bool positivePressed)
    {
        return (positivePressed ? 1f : 0f) - (negativePressed ? 1f : 0f);
    }

    private static void DrawPrompt(string message)
    {
        const float width = 360f;
        const float height = 42f;
        Rect panel = new((Screen.width - width) / 2f, Screen.height - 82f, width, height);
        Color previousColor = GUI.color;
        GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.9f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle style = new(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
        GUI.Label(panel, message, style);
        GUI.color = previousColor;
    }
}
