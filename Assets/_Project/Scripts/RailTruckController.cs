using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class RailTruckController : MonoBehaviour
{
    public enum JourneyState
    {
        ParkedAtDoc,
        DrivingToAntennas,
        ArrivedAtAntennas,
        ParkedAtAntennas,
        DrivingToDoc,
        ArrivedAtDoc,
    }

    [SerializeField] private FirstPersonPlayerController playerController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform truckRoot;
    [SerializeField] private Transform driverCameraPose;
    [SerializeField] private Transform departureInteractionPoint;
    [SerializeField] private Transform antennaExitPose;
    [SerializeField] private Transform[] routeWaypoints;
    [SerializeField, Min(1)] private int antennaStopWaypointIndex = 1;
    [SerializeField] private Transform[] wheels;
    [SerializeField, Min(0.1f)] private float speedMetersPerSecond = 5f;
    [SerializeField, Min(0.2f)] private float interactionDistanceMeters = 1.8f;
    [SerializeField, Min(0.05f)] private float fadeHalfSeconds = 0.32f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
    [SerializeField] private Vector2 lookAzimuthLimits = new(-160f, 160f);
    [SerializeField] private Vector2 lookElevationLimits = new(-70f, 80f);
    [SerializeField] private Vector2 zoomLimits = new(25f, 80f);
    [SerializeField, Min(0f)] private float scrollZoomDegreesPerNotch = 5f;
    [SerializeField, Min(0f)] private float zoomSmoothing = 12f;
    [SerializeField] private bool drawRouteGizmos = true;
    [SerializeField, Range(0f, 1f)] private float editorPreviewProgress;

    private readonly List<Vector3> routeSamples = new();
    private readonly List<float> routeDistances = new();
    private JourneyState state;
    private float travelledMeters;
    private float routeLength;
    private float antennaStopDistance;
    private bool transitioning;
    private bool cameraAttached;
    private Vector3 standingCameraLocalPosition;
    private Quaternion standingCameraLocalRotation;
    private float standingFieldOfView;
    private float lookAzimuth;
    private float lookElevation;
    private float targetFieldOfView;
    private CanvasGroup fadeCanvasGroup;

    public JourneyState State => state;
    public string StateName => state.ToString();
    public float Progress01
    {
        get
        {
            if (antennaStopDistance <= 0f || routeLength <= antennaStopDistance)
            {
                return 0f;
            }

            return travelledMeters <= antennaStopDistance
                ? travelledMeters / antennaStopDistance
                : 1f - (travelledMeters - antennaStopDistance)
                    / (routeLength - antennaStopDistance);
        }
    }
    public float RouteLengthMeters => routeLength;
    public float SpeedMetersPerSecond => speedMetersPerSecond;
    public bool DrawRouteGizmos => drawRouteGizmos;
    public float EditorPreviewProgress => editorPreviewProgress;

    public void Configure(
        FirstPersonPlayerController player,
        Transform truck,
        Transform cameraPose,
        Transform departurePoint,
        Transform exitPose,
        Transform[] waypoints,
        int antennaWaypointIndex,
        Transform[] wheelTransforms,
        float speed)
    {
        playerController = player;
        playerCamera = player != null ? player.PlayerCamera : null;
        truckRoot = truck;
        driverCameraPose = cameraPose;
        departureInteractionPoint = departurePoint;
        antennaExitPose = exitPose;
        routeWaypoints = waypoints;
        antennaStopWaypointIndex = Mathf.Clamp(
            antennaWaypointIndex,
            1,
            Mathf.Max(1, waypoints.Length - 2));
        wheels = wheelTransforms;
        speedMetersPerSecond = Mathf.Max(0.1f, speed);
        BuildRouteLookup();
        SetTruckAtDistance(0f, false);
    }

    public void SetDrawRouteGizmos(bool visible)
    {
        drawRouteGizmos = visible;
    }

    public void SetEditorPreviewProgress(float progress)
    {
        editorPreviewProgress = Mathf.Clamp01(progress);
        BuildRouteLookup();
        SetTruckAtDistance(routeLength * editorPreviewProgress, false);
    }

    private void Awake()
    {
        BuildRouteLookup();
        CreateFadeCanvas();
        state = JourneyState.ParkedAtDoc;
        travelledMeters = 0f;
        editorPreviewProgress = 0f;
        SetTruckAtDistance(0f, false);

        if (playerController == null || playerCamera == null || truckRoot == null
            || driverCameraPose == null || departureInteractionPoint == null
            || antennaExitPose == null || routeWaypoints == null
            || routeWaypoints.Length < 2)
        {
            Debug.LogError("Rail truck is missing required generated references.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (transitioning || RuntimeSceneSwitcher.IsOpen)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        switch (state)
        {
            case JourneyState.ParkedAtDoc:
                if (playerController.enabled
                    && PlayerIsNear(departureInteractionPoint)
                    && keyboard.fKey.wasPressedThisFrame)
                {
                    StartCoroutine(EnterTruck(true));
                }
                break;

            case JourneyState.DrivingToAntennas:
                UpdateDriverView();
                if (keyboard.wKey.isPressed)
                {
                    float previousDistance = travelledMeters;
                    travelledMeters = Mathf.Min(
                        antennaStopDistance,
                        travelledMeters + speedMetersPerSecond * Time.deltaTime);
                    SetTruckAtDistance(
                        travelledMeters,
                        true,
                        travelledMeters - previousDistance,
                        true);
                    if (travelledMeters >= antennaStopDistance - 0.001f)
                    {
                        state = JourneyState.ArrivedAtAntennas;
                    }
                }
                break;

            case JourneyState.ArrivedAtAntennas:
                UpdateDriverView();
                if (keyboard.fKey.wasPressedThisFrame)
                {
                    StartCoroutine(ExitTruck(
                        antennaExitPose,
                        JourneyState.ParkedAtAntennas));
                }
                else if (keyboard.wKey.isPressed)
                {
                    state = JourneyState.DrivingToDoc;
                }
                break;

            case JourneyState.ParkedAtAntennas:
                if (playerController.enabled
                    && PlayerIsNear(antennaExitPose)
                    && keyboard.fKey.wasPressedThisFrame)
                {
                    StartCoroutine(EnterTruck(false));
                }
                break;

            case JourneyState.DrivingToDoc:
                UpdateDriverView();
                if (keyboard.wKey.isPressed)
                {
                    float previousDistance = travelledMeters;
                    travelledMeters = Mathf.Max(
                        antennaStopDistance,
                        Mathf.Min(
                            routeLength,
                            travelledMeters + speedMetersPerSecond * Time.deltaTime));
                    SetTruckAtDistance(
                        travelledMeters,
                        true,
                        travelledMeters - previousDistance,
                        true);
                    if (travelledMeters >= routeLength - 0.001f)
                    {
                        state = JourneyState.ArrivedAtDoc;
                    }
                }
                break;

            case JourneyState.ArrivedAtDoc:
                UpdateDriverView();
                if (keyboard.fKey.wasPressedThisFrame)
                {
                    StartCoroutine(ExitTruck(
                        departureInteractionPoint,
                        JourneyState.ParkedAtDoc));
                }
                else if (keyboard.wKey.isPressed)
                {
                    // The route is a smooth closed loop. Wrap the scalar
                    // distance at the shared parking point and continue toward
                    // the antennas without teleporting or rotating the truck.
                    travelledMeters = 0f;
                    SetTruckAtDistance(travelledMeters, false);
                    state = JourneyState.DrivingToAntennas;
                }
                break;
        }
    }

    private void LateUpdate()
    {
        if (cameraAttached && playerCamera != null && driverCameraPose != null)
        {
            playerCamera.transform.SetPositionAndRotation(
                driverCameraPose.position,
                driverCameraPose.rotation
                    * Quaternion.Euler(lookElevation, lookAzimuth, 0f));
        }

        if (state == JourneyState.ParkedAtDoc
            && playerController != null
            && playerController.enabled
            && PlayerIsNear(departureInteractionPoint))
        {
            InteractionPromptDisplay.Show(this, "Press F to go outside");
        }
        else if ((state == JourneyState.DrivingToAntennas
                  || state == JourneyState.DrivingToDoc)
                 && !transitioning)
        {
            InteractionPromptDisplay.Show(
                this,
                "Hold W to drive | Mouse: look | Wheel: zoom");
        }
        else if (state == JourneyState.ArrivedAtAntennas && !transitioning)
        {
            InteractionPromptDisplay.Show(
                this,
                "Press F to exit truck | Press W to drive to building");
        }
        else if (state == JourneyState.ParkedAtAntennas
                 && playerController != null
                 && playerController.enabled
                 && PlayerIsNear(antennaExitPose))
        {
            InteractionPromptDisplay.Show(this, "Press F to get in truck");
        }
        else if (state == JourneyState.ArrivedAtDoc && !transitioning)
        {
            InteractionPromptDisplay.Show(
                this,
                "Press F to exit truck | Press W to drive to antennas");
        }
        else
        {
            InteractionPromptDisplay.Hide(this);
        }
    }

    private IEnumerator EnterTruck(bool drivingToAntennas)
    {
        transitioning = true;
        InteractionPromptDisplay.Hide(this);
        standingCameraLocalPosition = playerCamera.transform.localPosition;
        standingCameraLocalRotation = playerCamera.transform.localRotation;
        standingFieldOfView = playerCamera.fieldOfView;
        lookAzimuth = 0f;
        lookElevation = 0f;
        targetFieldOfView = Mathf.Clamp(
            playerCamera.fieldOfView,
            zoomLimits.x,
            zoomLimits.y);
        playerController.enabled = false;
        SetCursorCaptured(true);

        yield return FadeTo(1f);
        travelledMeters = drivingToAntennas ? 0f : antennaStopDistance;
        SetTruckAtDistance(travelledMeters, false);
        cameraAttached = true;
        playerCamera.transform.SetPositionAndRotation(
            driverCameraPose.position,
            driverCameraPose.rotation);
        playerCamera.fieldOfView = targetFieldOfView;
        yield return FadeTo(0f);

        state = drivingToAntennas
            ? JourneyState.DrivingToAntennas
            : JourneyState.DrivingToDoc;
        transitioning = false;
    }

    private void UpdateDriverView()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            lookAzimuth = Mathf.Clamp(
                lookAzimuth + mouseDelta.x * mouseSensitivity,
                lookAzimuthLimits.x,
                lookAzimuthLimits.y);
            lookElevation = Mathf.Clamp(
                lookElevation - mouseDelta.y * mouseSensitivity,
                lookElevationLimits.x,
                lookElevationLimits.y);

            float rawScroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(rawScroll) > 0.01f)
            {
                // Windows commonly reports 120 units per wheel notch; some
                // backends already report normalized notches.
                float scrollNotches = Mathf.Abs(rawScroll) > 10f
                    ? rawScroll / 120f
                    : rawScroll;
                targetFieldOfView = Mathf.Clamp(
                    targetFieldOfView - scrollNotches * scrollZoomDegreesPerNotch,
                    zoomLimits.x,
                    zoomLimits.y);
            }
        }

        float zoomT = 1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime);
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFieldOfView,
            zoomT);
    }

    private IEnumerator ExitTruck(Transform exitPose, JourneyState resultingState)
    {
        transitioning = true;
        InteractionPromptDisplay.Hide(this);
        yield return FadeTo(1f);

        cameraAttached = false;
        CharacterController characterController =
            playerController.GetComponent<CharacterController>();
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        playerController.transform.SetPositionAndRotation(
            exitPose.position,
            exitPose.rotation);
        playerCamera.transform.localPosition = standingCameraLocalPosition;
        playerCamera.transform.localRotation = standingCameraLocalRotation;
        playerCamera.fieldOfView = standingFieldOfView;

        if (characterController != null)
        {
            characterController.enabled = controllerWasEnabled;
        }

        yield return FadeTo(0f);
        state = resultingState;
        playerController.enabled = true;
        transitioning = false;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        fadeCanvasGroup.gameObject.SetActive(true);
        float startAlpha = fadeCanvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeHalfSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeHalfSeconds));
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
        if (targetAlpha <= 0f)
        {
            fadeCanvasGroup.gameObject.SetActive(false);
        }
    }

    private bool PlayerIsNear(Transform point)
    {
        return playerController != null && point != null
            && Vector3.Distance(playerController.transform.position, point.position)
                <= interactionDistanceMeters;
    }

    private void BuildRouteLookup()
    {
        routeSamples.Clear();
        routeDistances.Clear();
        routeLength = 0f;
        antennaStopDistance = 0f;
        if (routeWaypoints == null || routeWaypoints.Length < 2
            || routeWaypoints[0] == null)
        {
            return;
        }

        const int samplesPerSegment = 20;
        routeSamples.Add(routeWaypoints[0].position);
        routeDistances.Add(0f);
        for (int segment = 0; segment < routeWaypoints.Length - 1; segment++)
        {
            for (int sample = 1; sample <= samplesPerSegment; sample++)
            {
                float t = sample / (float)samplesPerSegment;
                Vector3 position = EvaluateSegment(segment, t);
                routeLength += Vector3.Distance(routeSamples[^1], position);
                routeSamples.Add(position);
                routeDistances.Add(routeLength);
            }

            if (segment + 1 == antennaStopWaypointIndex)
            {
                antennaStopDistance = routeLength;
            }
        }

        if (antennaStopDistance <= 0f)
        {
            antennaStopDistance = routeLength;
        }
    }

    private Vector3 EvaluateSegment(int segment, float t)
    {
        Vector3 p0 = routeWaypoints[Mathf.Max(0, segment - 1)].position;
        Vector3 p1 = routeWaypoints[segment].position;
        Vector3 p2 = routeWaypoints[segment + 1].position;
        Vector3 p3 = routeWaypoints[Mathf.Min(routeWaypoints.Length - 1, segment + 2)].position;
        return 0.5f * (
            2f * p1
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
    }

    private void SetTruckAtDistance(
        float distance,
        bool spinWheels,
        float distanceDelta = 0f,
        bool faceRouteForward = true)
    {
        if (truckRoot == null || routeSamples.Count < 2)
        {
            return;
        }

        distance = Mathf.Clamp(distance, 0f, routeLength);
        int upper = routeDistances.BinarySearch(distance);
        if (upper < 0)
        {
            upper = ~upper;
        }
        upper = Mathf.Clamp(upper, 1, routeDistances.Count - 1);
        int lower = upper - 1;
        float span = routeDistances[upper] - routeDistances[lower];
        float t = span > 0.0001f
            ? (distance - routeDistances[lower]) / span
            : 0f;
        Vector3 position = Vector3.Lerp(routeSamples[lower], routeSamples[upper], t);
        Vector3 forward = (routeSamples[upper] - routeSamples[lower]).normalized;
        if (!faceRouteForward)
        {
            forward = -forward;
        }
        truckRoot.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(forward, Vector3.up));

        if (spinWheels && wheels != null)
        {
            const float wheelRadius = 0.38f;
            float degrees = distanceDelta / (2f * Mathf.PI * wheelRadius) * 360f;
            foreach (Transform wheel in wheels)
            {
                if (wheel != null)
                {
                    wheel.Rotate(Vector3.up, degrees, Space.Self);
                }
            }
        }
    }

    private void CreateFadeCanvas()
    {
        GameObject canvasObject = new(
            "Truck Transition Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject imageObject = new(
            "Fade",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        fadeCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        canvasObject.SetActive(false);
    }

    private void OnDisable()
    {
        InteractionPromptDisplay.Hide(this);
    }

    private static void SetCursorCaptured(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    private void OnDrawGizmos()
    {
        if (!drawRouteGizmos || routeWaypoints == null || routeWaypoints.Length < 2)
        {
            return;
        }

        BuildRouteLookup();
        Gizmos.color = new Color(1f, 0.55f, 0.08f, 1f);
        for (int index = 1; index < routeSamples.Count; index++)
        {
            Gizmos.DrawLine(routeSamples[index - 1], routeSamples[index]);
        }
    }
}
