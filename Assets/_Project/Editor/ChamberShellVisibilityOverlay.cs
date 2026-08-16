using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[Overlay(typeof(SceneView), "ChamberShellVisibility", "Chamber Shell", defaultDisplay = true)]
public sealed class ChamberShellVisibilityOverlay : Overlay
{
    private Button toggleButton;

    public override VisualElement CreatePanelContent()
    {
        VisualElement root = new();
        toggleButton = new Button(ToggleShell)
        {
            tooltip = "Toggle the chamber and containing-room shells between visible and shadow-only.",
        };
        root.Add(toggleButton);
        root.schedule.Execute(RefreshLabel).Every(250);
        RefreshLabel();
        return root;
    }

    [MenuItem("Tools/Chamber/Toggle Shell Visibility %#v")]
    private static void ToggleShellFromMenu()
    {
        ToggleShellInActiveScene();
    }

    private void ToggleShell()
    {
        ToggleShellInActiveScene();
        RefreshLabel();
    }

    private static void ToggleShellInActiveScene()
    {
        ChamberShellVisibilityController controller = FindController();
        if (controller == null)
        {
            Debug.LogWarning("No ChamberShellVisibilityController was found in the active scene.");
            return;
        }

        if (!Application.isPlaying)
        {
            Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Toggle Chamber Shell Visibility");
        }

        controller.ToggleVisibility();
        EditorUtility.SetDirty(controller);
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        SceneView.RepaintAll();
    }

    private void RefreshLabel()
    {
        if (toggleButton == null)
        {
            return;
        }

        ChamberShellVisibilityController controller = FindController();
        toggleButton.SetEnabled(controller != null);
        toggleButton.text = controller == null
            ? "Shell unavailable"
            : controller.CutawayView ? "Shell: Cutaway" : "Shell: Opaque";
    }

    private static ChamberShellVisibilityController FindController()
    {
        return Object.FindFirstObjectByType<ChamberShellVisibilityController>(FindObjectsInactive.Include);
    }
}
