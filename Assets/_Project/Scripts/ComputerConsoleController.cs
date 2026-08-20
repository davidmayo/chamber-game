using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public sealed class ComputerConsoleController : MonoBehaviour
{
    private enum InteractionState
    {
        Standing,
        SittingDown,
        Seated,
        StandingUp,
    }

    [SerializeField] private FirstPersonPlayerController playerController;
    [SerializeField] private TurntableController turntableController;
    [SerializeField] private SourceAntennaController sourceAntennaController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform seatedCameraPose;
    [SerializeField, Min(0.05f)] private float transitionSeconds = 0.65f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
    [SerializeField] private Vector2 azimuthLimitsDegrees = new(-30f, 30f);
    [SerializeField] private Vector2 elevationLimitsDegrees = new(-30f, 15f);
    [SerializeField, Range(25f, 100f)] private float seatedDefaultFieldOfView = 75f;
    [SerializeField] private Vector2 seatedZoomLimits = new(25f, 75f);
    [SerializeField, Min(0f)] private float scrollZoomDegreesPerUnit = 0.05f;
    [SerializeField, Min(0f)] private float zoomSmoothing = 12f;

    private InteractionState state = InteractionState.Standing;
    private bool playerNearby;
    private float transitionElapsed;
    private Vector3 transitionStartPosition;
    private Quaternion transitionStartRotation;
    private Vector3 standingCameraLocalPosition;
    private Quaternion standingCameraLocalRotation;
    private float transitionStartFieldOfView;
    private float standingCameraFieldOfView;
    private float seatedTargetFieldOfView;
    private float seatedAzimuth;
    private float seatedElevation;

    public void Configure(
        FirstPersonPlayerController player,
        TurntableController turntable,
        SourceAntennaController sourceAntenna,
        Camera camera,
        Transform seatedPose)
    {
        playerController = player;
        turntableController = turntable;
        sourceAntennaController = sourceAntenna;
        playerCamera = camera;
        seatedCameraPose = seatedPose;
        if (turntableController != null)
        {
            turntableController.enabled = false;
        }
    }

    private void Awake()
    {
        if (playerController == null
            || turntableController == null
            || sourceAntennaController == null
            || playerCamera == null
            || seatedCameraPose == null)
        {
            Debug.LogError("ComputerConsoleController is missing required references.", this);
            enabled = false;
            return;
        }

        turntableController.enabled = false;
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
                UpdateStanding(keyboard);
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

    private void UpdateStanding(Keyboard keyboard)
    {
        if (!playerController.enabled)
        {
            return;
        }

        if (playerNearby && keyboard.fKey.wasPressedThisFrame)
        {
            BeginSittingDown();
            return;
        }

        if (Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame
            && Cursor.lockState != CursorLockMode.Locked)
        {
            SetCursorCaptured(true);
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
            seatedAzimuth = Mathf.Clamp(
                seatedAzimuth + mouseDelta.x * mouseSensitivity,
                azimuthLimitsDegrees.x,
                azimuthLimitsDegrees.y);
            seatedElevation = Mathf.Clamp(
                seatedElevation - mouseDelta.y * mouseSensitivity,
                elevationLimitsDegrees.x,
                elevationLimitsDegrees.y);
            playerCamera.transform.SetPositionAndRotation(
                seatedCameraPose.position,
                seatedCameraPose.rotation
                    * Quaternion.Euler(seatedElevation, seatedAzimuth, 0f));

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                seatedTargetFieldOfView = Mathf.Clamp(
                    seatedTargetFieldOfView - scroll * scrollZoomDegreesPerUnit,
                    seatedZoomLimits.x,
                    seatedZoomLimits.y);
            }
        }

        float zoomT = 1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime);
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            seatedTargetFieldOfView,
            zoomT);

        // Console operation intentionally exposes pan and tilt only. Height remains
        // unchanged and has no input binding in this mode.
        float panInput = ButtonAxis(keyboard.dKey.isPressed, keyboard.aKey.isPressed);
        float tiltInput = ButtonAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
        turntableController.ApplyInput(panInput, tiltInput, Time.deltaTime);
        float polarityInput = ButtonAxis(keyboard.qKey.isPressed, keyboard.eKey.isPressed);
        sourceAntennaController.ApplyInput(polarityInput, Time.deltaTime);
    }

    private void BeginSittingDown()
    {
        state = InteractionState.SittingDown;
        transitionElapsed = 0f;
        transitionStartPosition = playerCamera.transform.position;
        transitionStartRotation = playerCamera.transform.rotation;
        transitionStartFieldOfView = playerCamera.fieldOfView;
        standingCameraLocalPosition = playerCamera.transform.localPosition;
        standingCameraLocalRotation = playerCamera.transform.localRotation;
        standingCameraFieldOfView = playerCamera.fieldOfView;
        seatedTargetFieldOfView = Mathf.Clamp(
            seatedDefaultFieldOfView,
            seatedZoomLimits.x,
            seatedZoomLimits.y);
        seatedAzimuth = 0f;
        seatedElevation = 0f;
        playerController.enabled = false;
        turntableController.enabled = false;
        SetCursorCaptured(true);
    }

    private void BeginStandingUp()
    {
        state = InteractionState.StandingUp;
        transitionElapsed = 0f;
        transitionStartPosition = playerCamera.transform.position;
        transitionStartRotation = playerCamera.transform.rotation;
        transitionStartFieldOfView = playerCamera.fieldOfView;
        turntableController.enabled = false;
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
        float targetFieldOfView = sittingDown
            ? seatedTargetFieldOfView
            : standingCameraFieldOfView;
        playerCamera.fieldOfView = Mathf.Lerp(
            transitionStartFieldOfView,
            targetFieldOfView,
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
        if (!Application.isPlaying)
        {
            return;
        }

        if (playerController != null)
        {
            playerController.enabled = true;
        }
        if (turntableController != null)
        {
            turntableController.enabled = false;
        }
        SetCursorCaptured(false);
    }

    private void OnGUI()
    {
        if (state != InteractionState.Standing || !playerNearby)
        {
            return;
        }

        const float width = 300f;
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
        GUI.Label(panel, "Press F to control table", style);
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
