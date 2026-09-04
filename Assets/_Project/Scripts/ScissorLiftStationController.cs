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
        if (keyboard == null || RuntimeSceneSwitcher.IsOpen)
        {
            return;
        }

        if (!controllingLift)
        {
            if (playerController.enabled && playerNearby && keyboard.fKey.wasPressedThisFrame)
            {
                controllingLift = true;
                playerController.enabled = false;
                turntableController.enabled = false;
                SetCursorCaptured(true);
            }
            return;
        }

        if (keyboard.fKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
        {
            controllingLift = false;
            playerController.enabled = true;
            SetCursorCaptured(true);
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
        InteractionPromptDisplay.Hide(this);
        if (Application.isPlaying && controllingLift && playerController != null)
        {
            playerController.enabled = true;
        }
        controllingLift = false;
    }

    private void LateUpdate()
    {
        if (controllingLift)
        {
            InteractionPromptDisplay.Show(
                this,
                "Q lower / E raise     F or ESC to stop");
        }
        else if (playerNearby && playerController.enabled)
        {
            InteractionPromptDisplay.Show(this, "Press F to use scissor lift");
        }
        else
        {
            InteractionPromptDisplay.Hide(this);
        }
    }

    private static float ButtonAxis(bool negativePressed, bool positivePressed)
    {
        return (positivePressed ? 1f : 0f) - (negativePressed ? 1f : 0f);
    }

    private static void SetCursorCaptured(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

}
