using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Walk-through connection between the two facility scenes. Transitions use a
/// brief full-screen fade and named arrival markers on the safe side of doors.
/// </summary>
public sealed class FacilityScenePortal : MonoBehaviour
{
    [SerializeField] private string destinationScenePath;
    [SerializeField] private string destinationArrivalMarker;
    private bool loading;
    private BoxCollider portalBounds;

    private void Awake()
    {
        portalBounds = GetComponent<BoxCollider>();
    }

    public void Configure(string scenePath, string arrivalMarker)
    {
        destinationScenePath = scenePath;
        destinationArrivalMarker = arrivalMarker;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTransition(other.GetComponentInParent<FirstPersonPlayerController>());
    }

    private void Update()
    {
        if (loading || portalBounds == null)
        {
            return;
        }

        FirstPersonPlayerController player = FindFirstObjectByType<FirstPersonPlayerController>();
        if (player != null
            && portalBounds.bounds.Contains(player.transform.position + Vector3.up * 0.9f))
        {
            TryTransition(player);
        }
    }

    private void TryTransition(FirstPersonPlayerController player)
    {
        if (loading || player == null)
        {
            return;
        }

        int buildIndex = SceneUtility.GetBuildIndexByScenePath(destinationScenePath);
        if (buildIndex < 0)
        {
            Debug.LogError($"Facility portal destination is not enabled in Build Profiles: {destinationScenePath}", this);
            return;
        }

        loading = true;
        FacilityTransitionOverlay.Begin(buildIndex, destinationArrivalMarker);
    }
}

internal sealed class FacilityTransitionOverlay : MonoBehaviour
{
    private const float FadeSeconds = 0.32f;
    private CanvasGroup canvasGroup;
    private int destinationBuildIndex;
    private string arrivalMarker;

    public static void Begin(int buildIndex, string markerName)
    {
        FirstPersonPlayerController departingPlayer =
            FindFirstObjectByType<FirstPersonPlayerController>();
        if (departingPlayer != null)
        {
            departingPlayer.enabled = false;
        }

        GameObject overlay = new(
            "Facility Scene Transition",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup),
            typeof(Image),
            typeof(FacilityTransitionOverlay));
        DontDestroyOnLoad(overlay);

        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        Image image = overlay.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        FacilityTransitionOverlay transition = overlay.GetComponent<FacilityTransitionOverlay>();
        transition.destinationBuildIndex = buildIndex;
        transition.arrivalMarker = markerName;
        transition.canvasGroup = overlay.GetComponent<CanvasGroup>();
        transition.canvasGroup.alpha = 0f;
        transition.StartCoroutine(transition.Run());
    }

    private IEnumerator Run()
    {
        yield return Fade(0f, 1f);
        AsyncOperation load = SceneManager.LoadSceneAsync(destinationBuildIndex, LoadSceneMode.Single);
        while (!load.isDone)
        {
            yield return null;
        }
        yield return null;
        PlacePlayerAtArrival();
        yield return Fade(1f, 0f);
        Destroy(gameObject);
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < FadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / FadeSeconds);
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private void PlacePlayerAtArrival()
    {
        GameObject marker = GameObject.Find(arrivalMarker);
        FirstPersonPlayerController player = FindFirstObjectByType<FirstPersonPlayerController>();
        if (marker == null || player == null)
        {
            Debug.LogError($"Could not complete facility arrival at marker '{arrivalMarker}'.");
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
        player.transform.SetPositionAndRotation(marker.transform.position, marker.transform.rotation);
        if (controller != null)
        {
            controller.enabled = true;
        }
    }

}
