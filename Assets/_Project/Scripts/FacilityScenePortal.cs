using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Minimal walk-through scene connection used by the facility blockout.
/// A destination marker keeps the player from returning inside the arrival
/// portal and immediately bouncing back to the previous scene.
/// </summary>
public sealed class FacilityScenePortal : MonoBehaviour
{
    private static string pendingArrivalMarker;
    private static bool subscribed;

    [SerializeField] private string destinationScenePath;
    [SerializeField] private string destinationArrivalMarker;
    private bool loading;

    public void Configure(string scenePath, string arrivalMarker)
    {
        destinationScenePath = scenePath;
        destinationArrivalMarker = arrivalMarker;
    }

    private void Awake()
    {
        EnsureSceneLoadedSubscription();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (loading || other.GetComponentInParent<FirstPersonPlayerController>() == null)
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
        pendingArrivalMarker = destinationArrivalMarker;
        SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
    }

    private static void EnsureSceneLoadedSubscription()
    {
        if (subscribed)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        subscribed = true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(pendingArrivalMarker))
        {
            return;
        }

        RuntimeSceneSwitcher switcher = FindFirstObjectByType<RuntimeSceneSwitcher>();
        if (switcher != null)
        {
            switcher.StartCoroutine(PlacePlayerNextFrame(pendingArrivalMarker));
        }
    }

    private static IEnumerator PlacePlayerNextFrame(string markerName)
    {
        pendingArrivalMarker = null;
        yield return null;

        GameObject marker = GameObject.Find(markerName);
        FirstPersonPlayerController player = FindFirstObjectByType<FirstPersonPlayerController>();
        if (marker == null || player == null)
        {
            Debug.LogError($"Could not complete facility arrival at marker '{markerName}'.");
            yield break;
        }

        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }
        player.transform.SetPositionAndRotation(marker.transform.position, marker.transform.rotation);
        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }
}
