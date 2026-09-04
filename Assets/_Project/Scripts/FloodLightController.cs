using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class FloodLightController : MonoBehaviour
{
    [SerializeField] private FirstPersonPlayerController playerController;
    [SerializeField] private Light[] lights;
    [SerializeField] private Renderer[] illuminatedPanels;
    [SerializeField] private Material onMaterial;
    [SerializeField] private Material offMaterial;
    [SerializeField] private bool lightsOn;

    private bool playerNearby;

    public bool LightsOn => lightsOn;

    public void Configure(
        FirstPersonPlayerController player,
        Light[] controlledLights,
        Renderer[] panels,
        Material illuminatedMaterial,
        Material darkMaterial,
        bool initialLightsOn)
    {
        playerController = player;
        lights = controlledLights;
        illuminatedPanels = panels;
        onMaterial = illuminatedMaterial;
        offMaterial = darkMaterial;
        SetLights(initialLightsOn);
    }

    private void Awake()
    {
#if !UNITY_EDITOR
        ApplyStandaloneBuildDefaults();
#endif
        SetLights(lightsOn);
    }

    public void ApplyStandaloneBuildDefaults()
    {
        SetLights(ChamberBuildDefaults.FloodLightsOn);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (!RuntimeSceneSwitcher.IsOpen && playerController != null
            && playerController.enabled && playerNearby
            && keyboard != null && keyboard.fKey.wasPressedThisFrame)
        {
            SetLightsOn(!lightsOn);
        }
    }

    public void SetLightsOn(bool enabledState)
    {
        SetLights(enabledState);
    }

    private void SetLights(bool enabledState)
    {
        lightsOn = enabledState;
        if (lights != null)
        {
            foreach (Light controlledLight in lights)
            {
                if (controlledLight != null)
                {
                    controlledLight.enabled = enabledState;
                }
            }
        }

        if (illuminatedPanels != null)
        {
            foreach (Renderer panel in illuminatedPanels)
            {
                if (panel != null)
                {
                    panel.sharedMaterial = enabledState ? onMaterial : offMaterial;
                }
            }
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

    private void LateUpdate()
    {
        if (playerNearby && playerController != null && playerController.enabled)
        {
            InteractionPromptDisplay.Show(
                this,
                lightsOn
                    ? "Press F to turn off floodlights"
                    : "Press F to turn on floodlights");
        }
        else
        {
            InteractionPromptDisplay.Hide(this);
        }
    }

    private void OnDisable()
    {
        InteractionPromptDisplay.Hide(this);
    }
}
