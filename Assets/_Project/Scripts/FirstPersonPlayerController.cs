using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonPlayerController : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField, Min(0f)] private float moveSpeedMetersPerSecond = 2.5f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField] private Vector2 pitchLimitsDegrees = new(-80f, 80f);
    [SerializeField] private float gravityMetersPerSecondSquared = -9.81f;

    private CharacterController characterController;
    private float pitchDegrees;
    private float verticalSpeed;

    public Camera PlayerCamera => playerCamera;

    public void Configure(Camera camera)
    {
        playerCamera = camera;
        pitchDegrees = NormalizeAngle(camera.transform.localEulerAngles.x);
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (playerCamera == null)
        {
            Debug.LogError("FirstPersonPlayerController requires a player camera.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // The standing controller normally lets a click recapture the mouse.
        // Pause-menu clicks belong exclusively to the UI; recapturing here
        // moves/locks the pointer before uGUI can complete its click event.
        if (RuntimeSceneSwitcher.IsOpen)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame
            && Cursor.lockState != CursorLockMode.Locked)
        {
            SetCursorCaptured(true);
        }

        if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            ApplyMouseLook(Mouse.current.delta.ReadValue());
        }

        float strafe = ButtonAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
        float forward = ButtonAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
        Vector3 planarMovement = transform.right * strafe + transform.forward * forward;
        if (planarMovement.sqrMagnitude > 1f)
        {
            planarMovement.Normalize();
        }

        if (characterController.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }
        verticalSpeed += gravityMetersPerSecondSquared * Time.deltaTime;

        Vector3 velocity = planarMovement * moveSpeedMetersPerSecond;
        velocity.y = verticalSpeed;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void ApplyMouseLook(Vector2 mouseDelta)
    {
        transform.Rotate(0f, mouseDelta.x * mouseSensitivity, 0f, Space.Self);
        pitchDegrees = Mathf.Clamp(
            pitchDegrees - mouseDelta.y * mouseSensitivity,
            pitchLimitsDegrees.x,
            pitchLimitsDegrees.y);
        playerCamera.transform.localRotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
    }

    private static float ButtonAxis(bool negativePressed, bool positivePressed)
    {
        return (positivePressed ? 1f : 0f) - (negativePressed ? 1f : 0f);
    }

    private static float NormalizeAngle(float degrees)
    {
        return degrees > 180f ? degrees - 360f : degrees;
    }

    private static void SetCursorCaptured(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }
}
