using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class RidgeRecorderController : MonoBehaviour
{
    [SerializeField] private FirstPersonPlayerController player;
    [SerializeField] private Text status;
    [SerializeField] private Transform interactionPoint;
    [SerializeField, Min(0.1f)] private float downloadSeconds = 3f;
    private float elapsed;
    private bool downloading;

    public bool HasSnapshot { get; private set; }
    public float Progress01 => HasSnapshot ? 1f : Mathf.Clamp01(elapsed / downloadSeconds);
    public Transform InteractionPoint => interactionPoint;

    public void Configure(FirstPersonPlayerController controller, Transform point, Text display)
    {
        player = controller;
        interactionPoint = point;
        status = display;
    }

    public void ResetSnapshot()
    {
        HasSnapshot = false;
        downloading = false;
        elapsed = 0f;
    }

    private bool IsNearby => player != null && interactionPoint != null
        && Vector3.Distance(player.transform.position, interactionPoint.position) < 1.65f;

    private void Update()
    {
        if (RuntimeSceneSwitcher.IsOpen) return;
        bool available = IsNearby && player.enabled;
        if (downloading)
        {
            if (!available)
            {
                downloading = false;
                elapsed = 0f;
            }
            else
            {
                elapsed += Time.deltaTime;
                if (elapsed >= downloadSeconds)
                {
                    HasSnapshot = true;
                    downloading = false;
                }
            }
        }
        else if (available && !HasSnapshot && Keyboard.current != null
                 && Keyboard.current.fKey.wasPressedThisFrame)
        {
            downloading = true;
            elapsed = 0f;
        }

        if (status != null)
        {
            status.text = "RIDGE RECORDER / 07\n"
                + (HasSnapshot ? "SNAPSHOT COPIED\nReturn to operations"
                    : downloading ? $"COPYING {Progress01 * 100f:0}%\nKeep the reader connected"
                    : "LOCAL BUFFER READY\nF to copy snapshot");
        }
    }

    private void LateUpdate()
    {
        if (IsNearby && player.enabled)
        {
            InteractionPromptDisplay.Show(this, HasSnapshot
                ? "Recorder snapshot secured. Return to the DSN racks."
                : downloading ? $"Copying recorder snapshot... {Progress01 * 100f:0}%"
                : "Press F to copy the ridge recorder snapshot");
        }
        else InteractionPromptDisplay.Hide(this);
    }

    private void OnDisable() => InteractionPromptDisplay.Hide(this);
}
