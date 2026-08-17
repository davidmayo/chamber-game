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
        SetLights(lightsOn);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (playerNearby && keyboard != null && keyboard.fKey.wasPressedThisFrame)
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

    private void OnGUI()
    {
        if (!playerNearby)
        {
            return;
        }

        DrawPrompt(lightsOn
            ? "Press F to turn off floodlights"
            : "Press F to turn on floodlights");
    }

    private static void DrawPrompt(string message)
    {
        const float width = 340f;
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
        GUI.Label(panel, message, style);
        GUI.color = previousColor;
    }
}
