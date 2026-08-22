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
        Driving,
        Arrived,
        Completed,
    }

    [SerializeField] private FirstPersonPlayerController playerController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform truckRoot;
    [SerializeField] private Transform driverCameraPose;
    [SerializeField] private Transform departureInteractionPoint;
    [SerializeField] private Transform antennaExitPose;
    [SerializeField] private Transform[] routeWaypoints;
    [SerializeField] private Transform[] wheels;
    [SerializeField, Min(0.1f)] private float speedMetersPerSecond = 5f;
    [SerializeField, Min(0.2f)] private float interactionDistanceMeters = 1.8f;
    [SerializeField, Min(0.05f)] private float fadeHalfSeconds = 0.32f;
    [SerializeField] private bool drawRouteGizmos = true;
    [SerializeField, Range(0f, 1f)] private float editorPreviewProgress;

    private readonly List<Vector3> routeSamples = new();
    private readonly List<float> routeDistances = new();
    private JourneyState state;
    private float travelledMeters;
    private float routeLength;
    private bool transitioning;
    private bool cameraAttached;
    private Vector3 standingCameraLocalPosition;
    private Quaternion standingCameraLocalRotation;
    private float standingFieldOfView;
    private CanvasGroup fadeCanvasGroup;

    public JourneyState State => state;
    public string StateName => state.ToString();
    public float Progress01 => routeLength > 0f ? travelledMeters / routeLength : 0f;
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
                    StartCoroutine(EnterTruck());
                }
                break;

            case JourneyState.Driving:
                if (keyboard.wKey.isPressed)
                {
                    float previousDistance = travelledMeters;
                    travelledMeters = Mathf.Min(
                        routeLength,
                        travelledMeters + speedMetersPerSecond * Time.deltaTime);
                    SetTruckAtDistance(travelledMeters, true, travelledMeters - previousDistance);
                    if (travelledMeters >= routeLength - 0.001f)
                    {
                        state = JourneyState.Arrived;
                    }
                }
                break;

            case JourneyState.Arrived:
                if (keyboard.fKey.wasPressedThisFrame)
                {
                    StartCoroutine(ExitTruck());
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
                driverCameraPose.rotation);
        }

        if (state == JourneyState.ParkedAtDoc
            && playerController != null
            && playerController.enabled
            && PlayerIsNear(departureInteractionPoint))
        {
            InteractionPromptDisplay.Show(this, "Press F to go outside");
        }
        else if (state == JourneyState.Driving && !transitioning)
        {
            InteractionPromptDisplay.Show(this, "Hold W to drive to the antenna complex");
        }
        else if (state == JourneyState.Arrived && !transitioning)
        {
            InteractionPromptDisplay.Show(this, "Press F to exit truck");
        }
        else
        {
            InteractionPromptDisplay.Hide(this);
        }
    }

    private IEnumerator EnterTruck()
    {
        transitioning = true;
        InteractionPromptDisplay.Hide(this);
        standingCameraLocalPosition = playerCamera.transform.localPosition;
        standingCameraLocalRotation = playerCamera.transform.localRotation;
        standingFieldOfView = playerCamera.fieldOfView;
        playerController.enabled = false;

        yield return FadeTo(1f);
        travelledMeters = 0f;
        SetTruckAtDistance(0f, false);
        cameraAttached = true;
        playerCamera.transform.SetPositionAndRotation(
            driverCameraPose.position,
            driverCameraPose.rotation);
        yield return FadeTo(0f);

        state = JourneyState.Driving;
        transitioning = false;
    }

    private IEnumerator ExitTruck()
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
            antennaExitPose.position,
            antennaExitPose.rotation);
        playerCamera.transform.localPosition = standingCameraLocalPosition;
        playerCamera.transform.localRotation = standingCameraLocalRotation;
        playerCamera.fieldOfView = standingFieldOfView;

        if (characterController != null)
        {
            characterController.enabled = controllerWasEnabled;
        }

        yield return FadeTo(0f);
        state = JourneyState.Completed;
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

    private void SetTruckAtDistance(float distance, bool spinWheels, float distanceDelta = 0f)
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
