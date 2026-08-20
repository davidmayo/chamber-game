using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public sealed class GroundOpsDishConsoleController : MonoBehaviour
{
    private enum InteractionState
    {
        Standing,
        SittingDown,
        Seated,
        StandingUp,
    }

    [SerializeField] private FirstPersonPlayerController playerController;
    [SerializeField] private GroundOpsDishController dishController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform seatedCameraPose;
    [SerializeField, Min(0.05f)] private float transitionSeconds = 0.65f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
    [SerializeField] private Vector2 lookAzimuthLimits = new(-35f, 35f);
    [SerializeField] private Vector2 lookElevationLimits = new(-25f, 25f);
    [SerializeField, Range(25f, 100f)] private float seatedDefaultFieldOfView = 68f;
    [SerializeField] private Vector2 seatedZoomLimits = new(25f, 75f);
    [SerializeField, Min(0f)] private float scrollZoomDegreesPerUnit = 5f;
    [SerializeField, Min(0f)] private float zoomSmoothing = 12f;

    private InteractionState state;
    private bool playerNearby;
    private float transitionElapsed;
    private Vector3 transitionStartPosition;
    private Quaternion transitionStartRotation;
    private Vector3 standingCameraLocalPosition;
    private Quaternion standingCameraLocalRotation;
    private float seatedLookAzimuth;
    private float seatedLookElevation;
    private float standingCameraFieldOfView;
    private float transitionStartFieldOfView;
    private float seatedTargetFieldOfView;

    public void Configure(
        FirstPersonPlayerController player,
        GroundOpsDishController dishes,
        Camera camera,
        Transform seatedPose)
    {
        playerController = player;
        dishController = dishes;
        playerCamera = camera;
        seatedCameraPose = seatedPose;
        scrollZoomDegreesPerUnit = 5f;
    }

    private void Awake()
    {
        if (playerController == null
            || dishController == null
            || playerCamera == null
            || seatedCameraPose == null)
        {
            Debug.LogError("Ground Ops dish console is missing required references.", this);
            enabled = false;
            return;
        }
        SetCursorCaptured(true);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        switch (state)
        {
            case InteractionState.Standing:
                if (playerController.enabled && playerNearby && keyboard.fKey.wasPressedThisFrame)
                {
                    BeginSittingDown();
                }
                break;
            case InteractionState.SittingDown:
                UpdateTransition(true);
                break;
            case InteractionState.Seated:
                UpdateSeated(keyboard);
                break;
            case InteractionState.StandingUp:
                UpdateTransition(false);
                break;
        }
    }

    private void UpdateSeated(Keyboard keyboard)
    {
        if (keyboard.fKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
        {
            BeginStandingUp();
            return;
        }

        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            seatedLookAzimuth = Mathf.Clamp(
                seatedLookAzimuth + mouseDelta.x * mouseSensitivity,
                lookAzimuthLimits.x,
                lookAzimuthLimits.y);
            seatedLookElevation = Mathf.Clamp(
                seatedLookElevation - mouseDelta.y * mouseSensitivity,
                lookElevationLimits.x,
                lookElevationLimits.y);
            playerCamera.transform.SetPositionAndRotation(
                seatedCameraPose.position,
                seatedCameraPose.rotation
                    * Quaternion.Euler(seatedLookElevation, seatedLookAzimuth, 0f));

            float rawScroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(rawScroll) > 0.01f)
            {
                // Input System reports a wheel notch as either roughly 1 or
                // 120 depending on the platform/device. Normalize both to one
                // notch so the serialized value has a predictable meaning.
                float scrollNotches = Mathf.Abs(rawScroll) > 10f
                    ? rawScroll / 120f
                    : rawScroll;
                seatedTargetFieldOfView = Mathf.Clamp(
                    seatedTargetFieldOfView - scrollNotches * scrollZoomDegreesPerUnit,
                    seatedZoomLimits.x,
                    seatedZoomLimits.y);
            }
        }

        float zoomT = 1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime);
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            seatedTargetFieldOfView,
            zoomT);

        // Match the chamber console: A increases the horizontal angle, D
        // decreases it, W raises, and S lowers.
        float azimuthInput = ButtonAxis(keyboard.dKey.isPressed, keyboard.aKey.isPressed);
        float elevationInput = ButtonAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
        bool fineMode = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        bool fastMode = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        if (fineMode)
        {
            const float fineSpeedMultiplier = 0.2f;
            azimuthInput *= fineSpeedMultiplier;
            elevationInput *= fineSpeedMultiplier;
        }
        else if (fastMode)
        {
            const float fastSpeedMultiplier = 5f;
            azimuthInput *= fastSpeedMultiplier;
            elevationInput *= fastSpeedMultiplier;
        }
        dishController.ApplyInput(azimuthInput, elevationInput, Time.deltaTime);
    }

    private void BeginSittingDown()
    {
        state = InteractionState.SittingDown;
        transitionElapsed = 0f;
        transitionStartPosition = playerCamera.transform.position;
        transitionStartRotation = playerCamera.transform.rotation;
        standingCameraLocalPosition = playerCamera.transform.localPosition;
        standingCameraLocalRotation = playerCamera.transform.localRotation;
        standingCameraFieldOfView = playerCamera.fieldOfView;
        transitionStartFieldOfView = playerCamera.fieldOfView;
        seatedTargetFieldOfView = Mathf.Clamp(
            seatedDefaultFieldOfView,
            seatedZoomLimits.x,
            seatedZoomLimits.y);
        seatedLookAzimuth = 0f;
        seatedLookElevation = 0f;
        playerController.enabled = false;
        SetCursorCaptured(true);
    }

    private void BeginStandingUp()
    {
        state = InteractionState.StandingUp;
        transitionElapsed = 0f;
        transitionStartPosition = playerCamera.transform.position;
        transitionStartRotation = playerCamera.transform.rotation;
        transitionStartFieldOfView = playerCamera.fieldOfView;
    }

    private void UpdateTransition(bool sittingDown)
    {
        transitionElapsed += Time.unscaledDeltaTime;
        float linearT = Mathf.Clamp01(transitionElapsed / transitionSeconds);
        float smoothT = Mathf.SmoothStep(0f, 1f, linearT);
        Vector3 targetPosition = sittingDown
            ? seatedCameraPose.position
            : playerController.transform.TransformPoint(standingCameraLocalPosition);
        Quaternion targetRotation = sittingDown
            ? seatedCameraPose.rotation
            : playerController.transform.rotation * standingCameraLocalRotation;
        Vector3 position = Vector3.Lerp(transitionStartPosition, targetPosition, smoothT);
        position.y -= Mathf.Sin(linearT * Mathf.PI) * 0.06f;
        playerCamera.transform.SetPositionAndRotation(
            position,
            Quaternion.Slerp(transitionStartRotation, targetRotation, smoothT));
        playerCamera.fieldOfView = Mathf.Lerp(
            transitionStartFieldOfView,
            sittingDown ? seatedTargetFieldOfView : standingCameraFieldOfView,
            smoothT);

        if (linearT < 1f)
        {
            return;
        }

        if (sittingDown)
        {
            state = InteractionState.Seated;
            playerCamera.transform.SetPositionAndRotation(
                seatedCameraPose.position,
                seatedCameraPose.rotation);
            playerCamera.fieldOfView = seatedTargetFieldOfView;
        }
        else
        {
            state = InteractionState.Standing;
            playerCamera.transform.localPosition = standingCameraLocalPosition;
            playerCamera.transform.localRotation = standingCameraLocalRotation;
            playerCamera.fieldOfView = standingCameraFieldOfView;
            playerController.enabled = true;
        }
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
        if (Application.isPlaying && playerController != null)
        {
            playerController.enabled = true;
        }
    }

    private void OnGUI()
    {
        if (state != InteractionState.Standing || !playerNearby)
        {
            return;
        }

        const float width = 300f;
        Rect panel = new((Screen.width - width) / 2f, Screen.height - 82f, width, 42f);
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
        GUI.Label(panel, "Press F to sit at console", style);
        GUI.color = previousColor;
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
